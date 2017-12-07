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
using System.IO;

namespace Framework.CASC.FileSystem.Structures
{
    public class BuildConfig
    {
        public string[] this[string name]
        {
            get
            {
                string[] entry;

                if (entries.TryGetValue(name, out entry))
                    return entry;

                return null;
            }
        }

        Dictionary<string, string[]> entries = new Dictionary<string, string[]>();

        public BuildConfig(string wowPath, string buildKey)
        {
            using (var sr = new StreamReader($"{wowPath}/Data/config/{buildKey.GetHexAt(0)}/{buildKey.GetHexAt(2)}/{buildKey}"))
            {
                while (!sr.EndOfStream)
                {
                    var data = sr.ReadLine().Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

                    if (data.Length < 2)
                        continue;

                    var key = data[0].Trim();
                    var value = data[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    entries.Add(key, value);
                }
            }
        }
    }
}
