/*
 * Copyright (C) 2012-2017 CypherCore <http://github.com/CypherCore>
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Framework;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using Framework.Constants;
using System.Runtime.InteropServices;
using Framework.Collision;

namespace DataExtractor.Mmap
{
    using static Recast;
    using static Detour;

    class MapBuilder
    {
        public MapBuilder(VMapManager2 vm)
        {
            _vmapManager = vm;

            m_maxWalkableAngle = 70.0f;

            m_terrainBuilder = new TerrainBuilder(vm);
            m_rcContext = new rcContext(false);

            discoverTiles();
        }

        void discoverTiles()
        {
            uint mapID, tileX, tileY, tileID, count = 0;

            Console.WriteLine("Discovering maps... ");
            string[] files = Directory.GetFiles("maps").Select(Path.GetFileName).ToArray();
            for (uint i = 0; i < files.Length; ++i)
            {
                mapID = uint.Parse(files[i].Substring(0, 4));
                if (!m_tiles.Any(p => p.m_mapId == mapID))
                {
                    m_tiles.Add(new MapTiles(mapID));
                    count++;
                }
            }

            files = Directory.GetFiles("vmaps", "*.vmtree").Select(Path.GetFileName).ToArray();
            for (uint i = 0; i < files.Length; ++i)
            {
                mapID = uint.Parse(files[i].Substring(0, 4));
                if (!m_tiles.Any(p => p.m_mapId == mapID))
                {
                    m_tiles.Add(new MapTiles(mapID));
                    count++;
                }
            }
            Console.WriteLine($"found {count}");

            count = 0;
            Console.WriteLine("Discovering tiles... ");
            foreach (var mapTile in m_tiles)
            {
                var tiles = mapTile.m_tiles;
                mapID = mapTile.m_mapId;

                files = Directory.GetFiles("vmaps", $"{mapID:D4}_*.vmtile").Select(Path.GetFileName).ToArray();
                for (int i = 0; i < files.Length; ++i)
                {
                    tileX = uint.Parse(files[i].Substring(8, 2));
                    tileY = uint.Parse(files[i].Substring(5, 2));
                    tileID = StaticMapTree.packTileID(tileY, tileX);

                    tiles.Add(tileID);
                    count++;
                }

                files = Directory.GetFiles("maps", $"{mapID:D4}*").Select(Path.GetFileName).ToArray();
                for (uint i = 0; i < files.Length; ++i)
                {
                    tileY = uint.Parse(files[i].Substring(5, 2));
                    tileX = uint.Parse(files[i].Substring(8, 2));
                    tileID = StaticMapTree.packTileID(tileX, tileY);

                    if (tiles.Add(tileID))
                        count++;
                }
            }
            Console.WriteLine($"found {count}.\n");

            Interlocked.Add(ref m_totalTiles, (int)count);
        }

        SortedSet<uint> getTileList(uint mapID)
        {
            var mapTile = m_tiles.First(p => p.m_mapId == mapID);
            if (mapTile != null)
                return mapTile.m_tiles;

            m_tiles.Add(new MapTiles(mapID));
            return new SortedSet<uint>();
        }

        void getGridBounds(uint mapID, out uint minX, out uint minY, out uint maxX, out uint maxY)
        {
            maxX = int.MaxValue;
            maxY = int.MaxValue;
            minX = unchecked((uint)int.MinValue);
            minY = unchecked((uint)int.MinValue);

            float[] bmin = { 0, 0, 0 };
            float[] bmax = { 0, 0, 0 };
            float[] lmin = { 0, 0, 0 };
            float[] lmax = { 0, 0, 0 };
            MeshData meshData = new MeshData();

            // make sure we process maps which don't have tiles
            // initialize the static tree, which loads WDT models
            if (!m_terrainBuilder.loadVMap(mapID, 64, 64, meshData))
                return;

            // get the coord bounds of the model data
            if (meshData.solidVerts.Count + meshData.liquidVerts.Count == 0)
                return;

            // get the coord bounds of the model data
            if (meshData.solidVerts.Count != 0 && meshData.liquidVerts.Count != 0)
            {
                rcCalcBounds(meshData.solidVerts.ToArray(), meshData.solidVerts.Count / 3, bmin, bmax);
                rcCalcBounds(meshData.liquidVerts.ToArray(), meshData.liquidVerts.Count / 3, lmin, lmax);
                rcVmin(bmin, lmin);
                rcVmax(bmax, lmax);
            }
            else if (meshData.solidVerts.Count != 0)
                rcCalcBounds(meshData.solidVerts.ToArray(), meshData.solidVerts.Count / 3, bmin, bmax);
            else
                rcCalcBounds(meshData.liquidVerts.ToArray(), meshData.liquidVerts.Count / 3, lmin, lmax);

            // convert coord bounds to grid bounds
            maxX = (uint)(32 - bmin[0] / SharedConst.GRID_SIZE);
            maxY = (uint)(32 - bmin[2] / SharedConst.GRID_SIZE);
            minX = (uint)(32 - bmax[0] / SharedConst.GRID_SIZE);
            minY = (uint)(32 - bmax[2] / SharedConst.GRID_SIZE);
        }

        public void buildAllMaps()
        {
            m_tiles = m_tiles.OrderByDescending(a => a.m_tiles.Count).ToList();
            System.Threading.Tasks.Parallel.ForEach(m_tiles, mapTile =>
            {
                uint mapId = mapTile.m_mapId;
                if (!shouldSkipMap(mapId))
                    buildMap(mapId);
            });
        }

        void buildMap(uint mapID)
        {
            var tiles = getTileList(mapID);

            // make sure we process maps which don't have tiles
            if (tiles.Count == 0)
            {
                // convert coord bounds to grid bounds
                getGridBounds(mapID, out uint minX, out uint minY, out uint maxX, out uint maxY);

                // add all tiles within bounds to tile list.
                for (uint i = minX; i <= maxX; ++i)
                {
                    for (uint j = minY; j <= maxY; ++j)
                    {
                        uint packTileId = StaticMapTree.packTileID(i, j);
                        if (!tiles.Contains(packTileId))
                        {
                            tiles.Add(packTileId);
                            ++m_totalTiles;
                        }
                    }
                }
            }

            if (!tiles.Empty())
            {
                // build navMesh                
                dtNavMesh navMesh;
                buildNavMesh(mapID, out navMesh);
                if (navMesh == null)
                {
                    Console.WriteLine($"[Map {mapID:D4}] Failed creating navmesh!");
                    m_totalTilesProcessed += tiles.Count;
                    return;
                }

                // now start building mmtiles for each tile
                Console.WriteLine($"[Map {mapID:D4}] We have {tiles.Count} tiles.                          ");
                foreach (var it in tiles)
                {
                    // unpack tile coords
                    StaticMapTree.unpackTileID(it, out uint tileX, out uint tileY);

                    ++m_totalTilesProcessed;
                    if (shouldSkipTile(mapID, tileX, tileY))
                        continue;

                    buildTile(mapID, tileX, tileY, navMesh);
                }
            }

            Console.WriteLine($"[Map {mapID:D4}] Complete!");
        }

        void buildTile(uint mapID, uint tileX, uint tileY, dtNavMesh navMesh)
        {
            Console.WriteLine($"{m_totalTilesProcessed * 100 / m_totalTiles}% [Map {mapID:D4}] Building tile [{tileX:D2},{tileY:D2}]]");

            MeshData meshData = new MeshData();

            // get heightmap data
            m_terrainBuilder.loadMap(mapID, tileX, tileY, meshData, _vmapManager);

            // get model data
            m_terrainBuilder.loadVMap(mapID, tileY, tileX, meshData);

            // if there is no data, give up now
            if (meshData.solidVerts.Count == 0 && meshData.liquidVerts.Count == 0)
                return;

            // remove unused vertices
            TerrainBuilder.cleanVertices(meshData.solidVerts, meshData.solidTris);
            TerrainBuilder.cleanVertices(meshData.liquidVerts, meshData.liquidTris);

            // gather all mesh data for final data check, and bounds calculation
            float[] allVerts = new float[meshData.liquidVerts.Count + meshData.solidVerts.Count];
            Array.Copy(meshData.liquidVerts.ToArray(), allVerts, meshData.liquidVerts.Count);
            Array.Copy(meshData.solidVerts.ToArray(), 0, allVerts, meshData.liquidVerts.Count, meshData.solidVerts.Count);

            if (allVerts.Length == 0)
                return;

            // get bounds of current tile
            getTileBounds(tileX, tileY, allVerts, allVerts.Length / 3, out float[] bmin, out float[] bmax);

            m_terrainBuilder.loadOffMeshConnections(mapID, tileX, tileY, meshData, null);

            // build navmesh tile
            buildMoveMapTile(mapID, tileX, tileY, meshData, bmin, bmax, navMesh);
        }

        void buildNavMesh(uint mapID, out dtNavMesh navMesh)
        {
            // if map has a parent we use that to generate dtNavMeshParams - worldserver will load all missing tiles from that map
            int navMeshParamsMapId = _vmapManager.getParentMapId(mapID);
            if (navMeshParamsMapId == -1)
                navMeshParamsMapId = (int)mapID;

            SortedSet<uint> tiles = getTileList((uint)navMeshParamsMapId);

            // old code for non-statically assigned bitmask sizes:
            ///*** calculate number of bits needed to store tiles & polys ***/
            //int tileBits = dtIlog2(dtNextPow2(tiles.size()));
            //if (tileBits < 1) tileBits = 1;                                     // need at least one bit!
            //int polyBits = sizeof(dtPolyRef)*8 - SALT_MIN_BITS - tileBits;

            int polyBits = (int)DT_POLY_BITS;

            int maxTiles = tiles.Count;
            int maxPolysPerTile = 1 << polyBits;

            /***          calculate bounds of map         ***/

            uint tileXMin = 64, tileYMin = 64, tileXMax = 0, tileYMax = 0;
            foreach (var it in tiles)
            {
                StaticMapTree.unpackTileID(it, out uint tileX, out uint tileY);

                if (tileX > tileXMax)
                    tileXMax = tileX;
                else if (tileX < tileXMin)
                    tileXMin = tileX;

                if (tileY > tileYMax)
                    tileYMax = tileY;
                else if (tileY < tileYMin)
                    tileYMin = tileY;
            }

            // use Max because '32 - tileX' is negative for values over 32
            float[] bmin;
            float[] bmax;
            getTileBounds(tileXMax, tileYMax, null, 0, out bmin, out bmax);

            /***       now create the navmesh       ***/

            // navmesh creation params
            dtNavMeshParams navMeshParams = new dtNavMeshParams();
            navMeshParams.tileWidth = SharedConst.GRID_SIZE;
            navMeshParams.tileHeight = SharedConst.GRID_SIZE;
            rcVcopy(navMeshParams.orig, bmin);
            navMeshParams.maxTiles = maxTiles;
            navMeshParams.maxPolys = maxPolysPerTile;

            navMesh = new dtNavMesh();
            Console.WriteLine($"[Map {mapID:D4}] Creating navMesh...");
            if (dtStatusFailed(navMesh.init(navMeshParams)))
            {
                Console.WriteLine($"[Map {mapID:D4}] Failed creating navmesh!");
                return;
            }

            string fileName = $"mmaps/{mapID:D4}.mmap";
            using (BinaryWriter writer = new BinaryWriter(File.Open(fileName, FileMode.Create, FileAccess.Write)))
            {
                // now that we know navMesh params are valid, we can write them to file
                writer.Write(navMeshParams.orig[0]);
                writer.Write(navMeshParams.orig[1]);
                writer.Write(navMeshParams.orig[2]);
                writer.Write(navMeshParams.tileWidth);
                writer.Write(navMeshParams.tileHeight);
                writer.Write(navMeshParams.maxTiles);
                writer.Write(navMeshParams.maxPolys);
            }
        }

        void buildMoveMapTile(uint mapID, uint tileX, uint tileY, MeshData meshData, float[] bmin, float[] bmax, dtNavMesh navMesh)
        {
            // console output
            string tileString = $"[Map {mapID:D4}] [{tileX:D2},{tileY:D2}]: ";
            Console.WriteLine($"{tileString} Building movemap tiles...");

            Tile iv = new Tile();

            float[] tVerts = meshData.solidVerts.ToArray();
            int tVertCount = meshData.solidVerts.Count / 3;
            int[] tTris = meshData.solidTris.ToArray();
            int tTriCount = meshData.solidTris.Count / 3;

            float[] lVerts = meshData.liquidVerts.ToArray();
            int lVertCount = meshData.liquidVerts.Count / 3;
            int[] lTris = meshData.liquidTris.ToArray();
            int lTriCount = meshData.liquidTris.Count / 3;
            byte[] lTriFlags = meshData.liquidType.ToArray();

            // these are WORLD UNIT based metrics
            // this are basic unit dimentions
            // value have to divide GRID_SIZE(533.3333f) ( aka: 0.5333, 0.2666, 0.3333, 0.1333, etc )
            float BASE_UNIT_DIM = 0.2666666f;// m_bigBaseUnit ? 0.5333333f : 0.2666666f;

            // All are in UNIT metrics!
            int VERTEX_PER_MAP = (int)(SharedConst.GRID_SIZE / BASE_UNIT_DIM + 0.5f);
            int VERTEX_PER_TILE = 80;// m_bigBaseUnit ? 40 : 80; // must divide VERTEX_PER_MAP
            int TILES_PER_MAP = VERTEX_PER_MAP / VERTEX_PER_TILE;

            rcConfig config = new rcConfig();

            rcVcopy(config.bmin, bmin);
            rcVcopy(config.bmax, bmax);

            config.maxVertsPerPoly = DT_VERTS_PER_POLYGON;
            config.cs = BASE_UNIT_DIM;
            config.ch = BASE_UNIT_DIM;
            config.walkableSlopeAngle = m_maxWalkableAngle;
            config.tileSize = VERTEX_PER_TILE;
            config.walkableRadius = 2;// m_bigBaseUnit ? 1 : 2;
            config.borderSize = config.walkableRadius + 3;
            config.maxEdgeLen = VERTEX_PER_TILE + 1;        // anything bigger than tileSize
            config.walkableHeight = 6;// m_bigBaseUnit ? 3 : 6;
            // a value >= 3|6 allows npcs to walk over some fences
            // a value >= 4|8 allows npcs to walk over all fences
            config.walkableClimb = 8;// m_bigBaseUnit ? 4 : 8;
            config.minRegionArea = dtSqr(60);
            config.mergeRegionArea = dtSqr(50);
            config.maxSimplificationError = 1.8f;           // eliminates most jagged edges (tiny polygons)
            config.detailSampleDist = config.cs * 64;
            config.detailSampleMaxError = config.ch * 2;

            // this sets the dimensions of the heightfield - should maybe happen before border padding
            rcCalcGridSize(config.bmin, config.bmax, config.cs, out config.width, out config.height);

            // allocate subregions : tiles
            Tile[] tiles = new Tile[TILES_PER_MAP * TILES_PER_MAP];

            // Initialize per tile config.
            rcConfig tileCfg = new rcConfig(config);
            tileCfg.width = config.tileSize + config.borderSize * 2;
            tileCfg.height = config.tileSize + config.borderSize * 2;

            // merge per tile poly and detail meshes
            rcPolyMesh[] pmmerge = new rcPolyMesh[TILES_PER_MAP * TILES_PER_MAP];
            rcPolyMeshDetail[] dmmerge = new rcPolyMeshDetail[TILES_PER_MAP * TILES_PER_MAP];
            int nmerge = 0;
            // build all tiles
            for (int y = 0; y < TILES_PER_MAP; ++y)
            {
                for (int x = 0; x < TILES_PER_MAP; ++x)
                {
                    Tile tile = tiles[x + y * TILES_PER_MAP];

                    // Calculate the per tile bounding box.
                    tileCfg.bmin[0] = config.bmin[0] + (float)(x * config.tileSize - config.borderSize) * config.cs;
                    tileCfg.bmin[2] = config.bmin[2] + (float)(y * config.tileSize - config.borderSize) * config.cs;
                    tileCfg.bmax[0] = config.bmin[0] + (float)((x + 1) * config.tileSize + config.borderSize) * config.cs;
                    tileCfg.bmax[2] = config.bmin[2] + (float)((y + 1) * config.tileSize + config.borderSize) * config.cs;

                    // build heightfield
                    tile.solid = new rcHeightfield();
                    if (!rcCreateHeightfield(m_rcContext, tile.solid, tileCfg.width, tileCfg.height, tileCfg.bmin, tileCfg.bmax, tileCfg.cs, tileCfg.ch))
                    {
                        Console.WriteLine($"{tileString} Failed building heightfield!            ");
                        continue;
                    }

                    // mark all walkable tiles, both liquids and solids
                    byte[] triFlags = new byte[tTriCount];
                    for (var i = 0; i < tTriCount; ++i)
                        triFlags[i] = (byte)NavArea.Ground;

                    rcClearUnwalkableTriangles(m_rcContext, tileCfg.walkableSlopeAngle, tVerts, tVertCount, tTris, tTriCount, triFlags);
                    rcRasterizeTriangles(m_rcContext, tVerts, tVertCount, tTris, triFlags, tTriCount, tile.solid, config.walkableClimb);

                    rcFilterLowHangingWalkableObstacles(m_rcContext, config.walkableClimb, tile.solid);
                    rcFilterLedgeSpans(m_rcContext, tileCfg.walkableHeight, tileCfg.walkableClimb, tile.solid);
                    rcFilterWalkableLowHeightSpans(m_rcContext, tileCfg.walkableHeight, tile.solid);

                    rcRasterizeTriangles(m_rcContext, lVerts, lVertCount, lTris, lTriFlags, lTriCount, tile.solid, config.walkableClimb);

                    // compact heightfield spans
                    tile.chf = new rcCompactHeightfield();
                    if (!rcBuildCompactHeightfield(m_rcContext, tileCfg.walkableHeight, tileCfg.walkableClimb, tile.solid, tile.chf))
                    {
                        Console.WriteLine($"{tileString} Failed compacting heightfield!");
                        continue;
                    }

                    // build polymesh intermediates
                    if (!rcErodeWalkableArea(m_rcContext, config.walkableRadius, tile.chf))
                    {
                        Console.WriteLine($"{tileString} Failed eroding area!");
                        continue;
                    }

                    if (!rcBuildDistanceField(m_rcContext, tile.chf))
                    {
                        Console.WriteLine($"{tileString} Failed building distance field!");
                        continue;
                    }

                    if (!rcBuildRegions(m_rcContext, tile.chf, tileCfg.borderSize, tileCfg.minRegionArea, tileCfg.mergeRegionArea))
                    {
                        Console.WriteLine($"{tileString} Failed building regions!");
                        continue;
                    }

                    tile.cset = new rcContourSet();
                    if (!rcBuildContours(m_rcContext, tile.chf, tileCfg.maxSimplificationError, tileCfg.maxEdgeLen, tile.cset, 1))
                    {
                        Console.WriteLine($"{tileString} Failed building contours!");
                        continue;
                    }

                    // build polymesh
                    tile.pmesh = new rcPolyMesh();
                    if (!rcBuildPolyMesh(m_rcContext, tile.cset, tileCfg.maxVertsPerPoly, tile.pmesh))
                    {
                        Console.WriteLine($"{tileString} Failed building polymesh!");
                        continue;
                    }

                    tile.dmesh = new rcPolyMeshDetail();
                    if (!rcBuildPolyMeshDetail(m_rcContext, tile.pmesh, tile.chf, tileCfg.detailSampleDist, tileCfg.detailSampleMaxError, tile.dmesh))
                    {
                        Console.WriteLine($"{tileString} Failed building polymesh detail!");
                        continue;
                    }

                    // free those up
                    // we may want to keep them in the future for debug
                    // but right now, we don't have the code to merge them
                    tile.solid = null;
                    tile.chf = null;
                    tile.cset = null;

                    pmmerge[nmerge] = tile.pmesh;
                    dmmerge[nmerge] = tile.dmesh;
                    nmerge++;
                }
            }

            iv.pmesh = new rcPolyMesh();
            rcMergePolyMeshes(m_rcContext, ref pmmerge, nmerge, iv.pmesh);

            iv.dmesh = new rcPolyMeshDetail();
            rcMergePolyMeshDetails(m_rcContext, dmmerge, nmerge, ref iv.dmesh);


            // set polygons as walkable
            // TODO: special flags for DYNAMIC polygons, ie surfaces that can be turned on and off
            for (int i = 0; i < iv.pmesh.npolys; ++i)
            {
                byte area = (byte)(iv.pmesh.areas[i] & RC_WALKABLE_AREA);
                if (area != 0)
                {
                    if (area >= (byte)NavArea.MagmaSlime)
                        iv.pmesh.flags[i] = (ushort)(1 << (63 - area));
                    else
                        iv.pmesh.flags[i] = (byte)NavTerrainFlag.Ground; // TODO: these will be dynamic in future
                }
            }

            // setup mesh parameters
            dtNavMeshCreateParams createParams = new dtNavMeshCreateParams();
            //memset(&createParams, 0, sizeof(createParams));
            createParams.verts = iv.pmesh.verts;
            createParams.vertCount = iv.pmesh.nverts;
            createParams.polys = iv.pmesh.polys;
            createParams.polyAreas = iv.pmesh.areas;
            createParams.polyFlags = iv.pmesh.flags;
            createParams.polyCount = iv.pmesh.npolys;
            createParams.nvp = iv.pmesh.nvp;
            createParams.detailMeshes = iv.dmesh.meshes;
            createParams.detailVerts = iv.dmesh.verts;
            createParams.detailVertsCount = iv.dmesh.nverts;
            createParams.detailTris = iv.dmesh.tris;
            createParams.detailTriCount = iv.dmesh.ntris;

            createParams.offMeshConVerts = meshData.offMeshConnections.ToArray();
            createParams.offMeshConCount = meshData.offMeshConnections.Count / 6;
            createParams.offMeshConRad = meshData.offMeshConnectionRads.ToArray();
            createParams.offMeshConDir = meshData.offMeshConnectionDirs.ToArray();
            createParams.offMeshConAreas = meshData.offMeshConnectionsAreas.ToArray();
            createParams.offMeshConFlags = meshData.offMeshConnectionsFlags.ToArray();

            createParams.walkableHeight = BASE_UNIT_DIM * config.walkableHeight;    // agent height
            createParams.walkableRadius = BASE_UNIT_DIM * config.walkableRadius;    // agent radius
            createParams.walkableClimb = BASE_UNIT_DIM * config.walkableClimb;      // keep less that walkableHeight (aka agent height)!
            createParams.tileX = (int)((((bmin[0] + bmax[0]) / 2) - navMesh.getParams().orig[0]) / SharedConst.GRID_SIZE);
            createParams.tileY = (int)((((bmin[2] + bmax[2]) / 2) - navMesh.getParams().orig[2]) / SharedConst.GRID_SIZE);
            rcVcopy(createParams.bmin, bmin);
            rcVcopy(createParams.bmax, bmax);
            createParams.cs = config.cs;
            createParams.ch = config.ch;
            createParams.tileLayer = 0;
            createParams.buildBvTree = true;

            // will hold final navmesh
            dtRawTileData navData = null;

            do
            {
                // these values are checked within dtCreateNavMeshData - handle them here
                // so we have a clear error message
                if (createParams.nvp > DT_VERTS_PER_POLYGON)
                {
                    Console.WriteLine($"{tileString} Invalid verts-per-polygon value!");
                    break;
                }
                if (createParams.vertCount >= 0xffff)
                {
                    Console.WriteLine($"{tileString} Too many vertices!");
                    break;
                }
                if (createParams.vertCount == 0 || createParams.verts == null)
                {
                    // occurs mostly when adjacent tiles have models
                    // loaded but those models don't span into this tile

                    // message is an annoyance
                    break;
                }
                if (createParams.polyCount == 0 || createParams.polys == null ||
                    TILES_PER_MAP * TILES_PER_MAP == createParams.polyCount)
                {
                    // we have flat tiles with no actual geometry - don't build those, its useless
                    // keep in mind that we do output those into debug info
                    // drop tiles with only exact count - some tiles may have geometry while having less tiles
                    Console.WriteLine($"{tileString} No polygons to build on tile!              ");
                    break;
                }
                if (createParams.detailMeshes == null || createParams.detailVerts == null || createParams.detailTris == null)
                {
                    Console.WriteLine($"{tileString} No detail mesh to build tile!");
                    break;
                }

                Console.WriteLine($"{tileString} Building navmesh tile...");
                if (!dtCreateNavMeshData(createParams, out navData))
                {
                    Console.WriteLine($"{tileString} Failed building navmesh tile!");
                    break;
                }
                
                ulong tileRef = 0;
                Console.WriteLine($"{tileString} Adding tile to navmesh...");
                // DT_TILE_FREE_DATA tells detour to unallocate memory when the tile
                // is removed via removeTile()
                if (dtStatusFailed(navMesh.addTile(navData, (int)dtTileFlags.DT_TILE_FREE_DATA, 0, ref tileRef)) || tileRef == 0)
                {
                    Console.WriteLine($"{tileString} Failed adding tile to navmesh!");
                    break;
                }

                // file output
                string fileName = $"mmaps/{mapID:D4}{tileY:D2}{tileX:D2}.mmtile";
                using (BinaryWriter binaryWriter = new BinaryWriter(File.Open(fileName, FileMode.Create, FileAccess.Write)))
                {
                    Console.WriteLine($"{tileString} Writing to file...");

                    var navDataBytes = navData.ToBytes();

                    // write header
                    MmapTileHeader header = new MmapTileHeader();
                    header.mmapMagic = SharedConst.MMAP_MAGIC;
                    header.dtVersion = Detour.DT_NAVMESH_VERSION;
                    header.mmapVersion = SharedConst.MMAP_VERSION;
                    header.usesLiquids = m_terrainBuilder.usesLiquids();
                    header.size = (uint)navDataBytes.Length;
                    binaryWriter.WriteStruct(header);

                    // write data
                    binaryWriter.Write(navDataBytes);
                }

                // now that tile is written to disk, we can unload it
                navMesh.removeTile(tileRef, out dtRawTileData dtRawTileData);
            }
            while (false);
        }

        void getTileBounds(uint tileX, uint tileY, float[] verts, int vertCount, out float[] bmin, out float[] bmax)
        {
            bmin = new float[3];
            bmax = new float[3];

            // this is for elevation
            if (verts != null && vertCount != 0)
                rcCalcBounds(verts, vertCount, bmin, bmax);
            else
            {
                bmin[1] = float.MinValue;
                bmax[1] = float.MaxValue;
            }

            // this is for width and depth
            bmax[0] = (32 - (int)tileX) * SharedConst.GRID_SIZE;
            bmax[2] = (32 - (int)tileY) * SharedConst.GRID_SIZE;
            bmin[0] = bmax[0] - SharedConst.GRID_SIZE;
            bmin[2] = bmax[2] - SharedConst.GRID_SIZE;
        }

        bool shouldSkipMap(uint mapID)
        {
            switch (mapID)
            {
                case 13:    // test.wdt
                case 25:    // ScottTest.wdt
                case 29:    // Test.wdt
                case 42:    // Colin.wdt
                case 169:   // EmeraldDream.wdt (unused, and very large)
                case 451:   // development.wdt
                case 573:   // ExteriorTest.wdt
                case 597:   // CraigTest.wdt
                case 605:   // development_nonweighted.wdt
                case 606:   // QA_DVD.wdt
                case 651:   // ElevatorSpawnTest.wdt
                case 1060:  // LevelDesignLand-DevOnly.wdt
                case 1181:  // PattyMackTestGarrisonBldgMap.wdt
                case 1264:  // Propland-DevOnly.wdt
                case 1270:  // devland3.wdt
                case 1310:  // Expansion5QAModelMap.wdt
                case 1407:  // GorgrondFinaleScenarioMap.wdt (zzzOld)
                case 1427:  // PattyMackTestGarrisonBldgMap2.wdt
                case 1451:  // TanaanLegionTest.wdt
                case 1454:  // ArtifactAshbringerOrigin.wdt
                case 1457:  // FXlDesignLand-DevOnly.wdt
                case 1471:  // 1466.wdt (Dungeon Test Map 1466)
                case 1499:  // Artifact-Warrior Fury Acquisition.wdt (oldArtifact - Warrior Fury Acquisition)
                case 1537:  // BoostExperience.wdt (zzOLD - Boost Experience)
                case 1538:  // Karazhan Scenario.wdt (test)
                case 1549:  // TechTestSeamlessWorldTransitionA.wdt
                case 1550:  // TechTestSeamlessWorldTransitionB.wdt
                case 1555:  // TransportBoostExperienceAllianceGunship.wdt
                case 1556:  // TransportBoostExperienceHordeGunship.wdt
                case 1561:  // TechTestCosmeticParentPerformance.wdt
                case 1582:  // Artifact�DalaranVaultAcquisition.wdt // no, this weird symbol is not an encoding error.
                case 1584:  // JulienTestLand-DevOnly.wdt
                case 1586:  // AssualtOnStormwind.wdt (Assault on Stormwind - Dev Map)
                case 1588:  // DevMapA.wdt
                case 1589:  // DevMapB.wdt
                case 1590:  // DevMapC.wdt
                case 1591:  // DevMapD.wdt
                case 1592:  // DevMapE.wdt
                case 1593:  // DevMapF.wdt
                case 1594:  // DevMapG.wdt
                case 1603:  // AbyssalMaw_Interior_Scenario.wdt
                case 1670:  // BrokenshorePristine.wdt
                    return true;
                default:
                    if (isTransportMap(mapID))
                        return true;
                    break;
            }

            return false;
        }

        bool isTransportMap(uint mapID)
        {
            switch (mapID)
            {
                // transport maps
                case 582:
                case 584:
                case 586:
                case 587:
                case 588:
                case 589:
                case 590:
                case 591:
                case 592:
                case 593:
                case 594:
                case 596:
                case 610:
                case 612:
                case 613:
                case 614:
                case 620:
                case 621:
                case 622:
                case 623:
                case 641:
                case 642:
                case 647:
                case 662:
                case 672:
                case 673:
                case 674:
                case 712:
                case 713:
                case 718:
                case 738:
                case 739:
                case 740:
                case 741:
                case 742:
                case 743:
                case 747:
                case 748:
                case 749:
                case 750:
                case 762:
                case 763:
                case 765:
                case 766:
                case 767:
                case 1113:
                case 1132:
                case 1133:
                case 1172:
                case 1173:
                case 1192:
                case 1231:
                case 1459:
                case 1476:
                case 1484:
                case 1555:
                case 1556:
                case 1559:
                case 1560:
                case 1628:
                case 1637:
                case 1638:
                case 1639:
                case 1649:
                case 1650:
                case 1711:
                case 1751:
                case 1752:
                case 1856:
                case 1857:
                case 1902:
                case 1903:
                    return true;
                default:
                    return false;
            }
        }

        bool shouldSkipTile(uint mapID, uint tileX, uint tileY)
        {
            string fileName = $"mmaps/{mapID:D4}{tileY:D2}{tileX:D2}.mmtile";
            if (!File.Exists(fileName))
                return false;

            using (BinaryReader reader = new BinaryReader(File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                MmapTileHeader header = reader.Read<MmapTileHeader>();
                if (header.mmapMagic != SharedConst.MMAP_MAGIC || header.dtVersion != DT_NAVMESH_VERSION)
                    return false;

                if (header.mmapVersion != SharedConst.MMAP_VERSION)
                    return false;
            }
            return true;
        }

        TerrainBuilder m_terrainBuilder;
        List<MapTiles> m_tiles = new List<MapTiles>();

        float m_maxWalkableAngle;

        volatile int m_totalTiles;
        volatile int m_totalTilesProcessed;

        // build performance - not really used for now
        rcContext m_rcContext;

        VMapManager2 _vmapManager;
    }

    struct MmapTileHeader
    {
        public uint mmapMagic;
        public uint dtVersion;
        public uint mmapVersion;
        public uint size;
        public bool usesLiquids;
        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        //public byte[] padding;//3
    }

    class MapTiles
    {
        public MapTiles(uint id = 0xFFFFFFFF)
        {
            m_mapId = id;
        }

        public MapTiles(uint id, SortedSet<uint> tiles)
        {
            m_mapId = id;
            m_tiles = tiles;
        }

        public uint m_mapId;
        public SortedSet<uint> m_tiles = new SortedSet<uint>();
    }

    struct Tile
    {
        public rcCompactHeightfield chf;
        public rcHeightfield solid;
        public rcContourSet cset;
        public rcPolyMesh pmesh;
        public rcPolyMeshDetail dmesh;
    }
}
