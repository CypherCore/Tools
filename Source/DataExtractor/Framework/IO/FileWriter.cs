﻿/*
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

using System.IO;

public class FileWriter
{
    public static void WriteFile(Stream data, string path, FileMode fileMode = FileMode.Create)
    {
        using MemoryStream ms = new();
        using var fs = new FileStream(path, fileMode, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, true);
        data.CopyTo(ms);
        fs.Write(ms.ToArray(), 0, (int)data.Length);
    }
}
