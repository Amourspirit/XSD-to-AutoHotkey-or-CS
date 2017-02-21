using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using BigByteTechnologies.XsdOut.AutoHotkey;

namespace BigByteTechnologies.XsdOut
{
    partial class Program
    {
        static void Main(string[] args)
        {
            var options = new Options();
            options.Import = false;
            options.Export = false;

            

            var parser = new CommandLine.Parser(with => with.HelpWriter = Console.Error);

            if (parser.ParseArgumentsStrict(args, options, () => Environment.Exit(-2)))
            {
                Run(options);
            }
           
                                 
        }

        private static void Run(Options options)
        {
            if (options.Classes)
            {
                if (string.IsNullOrEmpty(options.Xsd))
                {
                    Console.WriteLine("XSD file is required to generate classes");
                    Environment.Exit(-2);

                }
                string xsdFile = options.Xsd;

                if (xsdFile.IndexOf(Path.DirectorySeparatorChar) < 0)
                {
                    string path = Directory.GetCurrentDirectory();
                    xsdFile = Path.Combine(path, xsdFile);
                }

                if (!File.Exists(xsdFile))
                {
                    Console.WriteLine("XSD file is not an existing file");
                    Environment.Exit(-2);
                }
                string fileExt = Path.GetExtension(options.Xsd);
                if (string.Equals(fileExt, ".xsd", StringComparison.CurrentCultureIgnoreCase) == false)
                {
                    Console.WriteLine("XSD file is not a valid xsd type of file.");
                    Environment.Exit(-2);
                }
                if (options.Lang == LanguageOption.NONE)
                {
                    Console.WriteLine("language option must be set to generate classes");
                    Environment.Exit(-2);
                }
                string outDir = options.OutDir;
                if (!string.IsNullOrEmpty(outDir))
                {
                    if (outDir.IndexOf(Path.DirectorySeparatorChar) < 0)
                    {
                        string path = Directory.GetCurrentDirectory();
                        outDir = Path.Combine(path, outDir);
                    }

                    if (!Directory.Exists(outDir))
                    {
                        outDir = string.Empty;
                    }
                }
                if (string.IsNullOrEmpty(outDir))
                {
                    outDir = Directory.GetCurrentDirectory();
                }
                string fileNoExt = Path.GetFileNameWithoutExtension(xsdFile);
                string outFile = string.Empty;
                switch (options.Lang)
                {
                    case LanguageOption.CSHARP:
                        outFile = Path.Combine(outDir, fileNoExt + ".cs");
                        var xCs = new XsdToCS(xsdFile);
                        xCs.Convert();
                        string csCode = xCs.Code;

                        File.WriteAllText(outFile, csCode);
                        break;
                    case LanguageOption.AUTOHOTKEY:
                        outFile = Path.Combine(outDir, fileNoExt + ".ahk");
                        AutoHotkeyBuilder ab = new AutoHotkeyBuilder(xsdFile);
                        ab.Import = options.Import;
                        ab.Export = options.Export;
                        ab.XmlIgnoreWhiteSpace = options.XmlIgnoreWhiteSpace;
                        ab.IncludeXmlHelper = options.XmlHelper;
                        ab.Build();
                        File.WriteAllText(outFile, ab.ToString());
                        break;
                    case LanguageOption.NONE:
                    default:
                        break;
                }

            }
        }
    }

    
}
