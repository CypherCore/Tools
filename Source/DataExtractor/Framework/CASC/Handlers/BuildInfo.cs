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

namespace Framework.CASC.Handlers
{
    public class BuildInfo
    {
        public string this[string name]
        {
            get
            {
                string entry;

                if (entries.TryGetValue(name, out entry))
                    return entry;

                return null;
            }
        }

        Dictionary<string, string> entries = new Dictionary<string, string>();

        public BuildInfo(string file)
        {
            using (var sr = new StreamReader(file))
            {
                var header = sr.ReadLine().Split(new[] { '|', '!' });
                var dataLine = "";

                while ((dataLine = sr.ReadLine()) != null)
                {
                    var data = dataLine.Split(new[] { '|' });

                    if (data.Length != header.Length / 2)
                        throw new InvalidOperationException("bla...");

                    // Be sure to get the active build info.
                    if (data[1] == "0")
                        continue;

                    for (var i = 0; i < data.Length; i++)
                        entries.Add(header[i << 1], data[i]);
                }
            }
        }
    }
}
