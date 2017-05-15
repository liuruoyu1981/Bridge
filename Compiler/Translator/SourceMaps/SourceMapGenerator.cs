﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Bridge.Contract;

namespace Bridge.Translator
{
	public class SourceMapGenerator : ISourceMapRecorder
    {
		private readonly SourceMapBuilder _sourceMapBuilder;

		public SourceMapGenerator(string scriptPath, string sourceRoot) {
			string scriptFileName = Path.GetFileName(scriptPath);
			_sourceMapBuilder = new SourceMapBuilder(scriptFileName, sourceRoot);
		}

		public void RecordLocation(int scriptLine, int scriptCol, string sourcePath, int sourceLine, int sourceCol) {
			SourceLocation sourceLocation;
			if (sourcePath == null) {
				sourceLocation = new SourceLocation("no-source-location", "", 0, 0);
			}
			else {
				sourceLocation = new SourceLocation(sourcePath, "", sourceLine - 1, sourceCol - 1);    // convert line and column to 0-based
			}

			_sourceMapBuilder.AddMapping(scriptLine - 1, scriptCol - 1, sourceLocation);
		}

		public string GetSourceMap(string[] sourcesContent)
        {
			return _sourceMapBuilder.Build(sourcesContent);
		}

        private static Regex tokenRegex = new Regex(@"/\*##\|(.+?),(\d+?),(\d+?)\|##\*/", RegexOptions.Compiled);
        public static void Generate(string scriptFileName, string basePath, string outputPath, string root, ref string content, Action<string, string> saveAction, Func<string, string> sourceContent, string[] names, IList<string> sourceFiles)
        {
            var fileName = Path.GetFileName(scriptFileName);
            var generator = new SourceMapGenerator(fileName, "");
            StringLocation location = null;
            string script = content;
            int offset = 0;
            content = tokenRegex.Replace(content, match =>
            {
                location = SourceMapGenerator.LocationFromPos(script, match.Index, location, ref offset);
                offset += match.Length;
                var sourceLine = int.Parse(match.Groups[2].Value);
                var sourceCol = int.Parse(match.Groups[3].Value);
                var sourcePath = sourceFiles[int.Parse(match.Groups[1].Value)];
                sourcePath = sourcePath.Substring(basePath.Length + 1);

                generator.RecordLocation(location.Line, location.Column, sourcePath, sourceLine, sourceCol);
                return "";
            });
            var sources = generator._sourceMapBuilder.SourceUrlList;
            List<string> contents = new List<string>();

            foreach (var source in sources)
            {
                contents.Add(sourceContent(source));
            }

            // Chrome handles it very strange, need more investigate
            //generator._sourceMapBuilder.SourceNameList.AddRange(names);
            var map = generator.GetSourceMap(contents.ToArray());
            content = content + Emitter.NEW_LINE + "//# sourceMappingURL=data:application/json;base64," + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(map));

            /*fileName = fileName + ".map";

            if (root != null)
            {
                fileName = new ConfigHelper().ConvertPath(Path.Combine(root, fileName), '/');
            }

            

            fileName = new ConfigHelper().ConvertPath(Path.Combine(outputPath, fileName), '/');
            var file = new System.IO.FileInfo(fileName);

            if (!file.Directory.Exists)
            {
                file.Directory.Create();
            }

            saveAction(fileName, map);*/
        }

        private static StringLocation LocationFromPos(string s, int pos, StringLocation lastLocation, ref int offset)
        {
            int res = lastLocation?.Line ?? 1;
            int startLinePosition = lastLocation?.StartLinePosition ?? 0;
            int i;
            for (i = lastLocation?.Position ?? 0; i <= pos - 1; i++)
            {
                if (s[i] == '\n')
                {
                    startLinePosition = i;
                    offset = 0;
                    res++;
                }
            }
            return new StringLocation(res, pos - startLinePosition - offset, i, startLinePosition);
        }

        private class StringLocation
        {
            public StringLocation(int line, int column, int position, int startLinePosition)
            {
                this.Line = line;
                this.Column = column;
                this.StartLinePosition = startLinePosition;
                this.Position = position;
            }

            public int Line { get; set; }
            public int Column { get; set; }
            public int StartLinePosition { get; set; }
            public int Position { get; set; }
        }
    }
}