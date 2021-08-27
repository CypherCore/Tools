/*
 * Copyright (C) 2012-2019 CypherCore <http://github.com/CypherCore>
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

using DataExtractor.Framework.GameMath;
using System.Collections.Generic;
using System.Numerics;

namespace DataExtractor.Framework.Collision
{
    public class TriBoundFunc
    {
        public TriBoundFunc(List<Vector3> vert)
        {
            vertices = vert;
        }

        public void Invoke(MeshTriangle tri, out AxisAlignedBox value)
        {
            Vector3 lo = vertices[(int)tri.idx0];
            Vector3 hi = lo;

            lo = Vector3.Min(Vector3.Min(lo, vertices[(int)tri.idx1]), vertices[(int)tri.idx2]);
            hi = Vector3.Max(Vector3.Max(hi, vertices[(int)tri.idx1]), vertices[(int)tri.idx2]);

            value = new AxisAlignedBox(lo, hi);
        }

        List<Vector3> vertices;
    }
}
