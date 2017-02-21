using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommandLine;
using CommandLine.Text;
using System.Text.RegularExpressions;

namespace BigByteTechnologies.XsdOut
{
    partial class Program
    {
        private enum LanguageOption
        {
            NONE,
            CSHARP,
            AUTOHOTKEY

        }

        private sealed class Options
        {
            private bool m_LangSet = false;
            private LanguageOption m_lang = LanguageOption.NONE;

            [ValueOption(0)]
            public string Xsd { get; set; }

            [Option('c', "classes", Required = false, HelpText = "Generate Classes for this Schema")]
            public bool Classes { get; set; }

            [Option('p', "xmlhelper", Required = false, HelpText = "Append XmlHelper Class to AutoHotKey Output")]
            public bool XmlHelper { get; set; }

            [Option('l',"language", Required = false,DefaultValue = "CS", HelpText = "The language to use for the generated code. Choose from 'CS' or 'AHK'")]
            public string Language { get; set; }

            [Option('o',"out", Required = false, HelpText = "The output directory to put files in. The default is the current Directory.")]
            public string OutDir { get; set; }

            [Option('i',"import", HelpText = "If true then import will be included for AutoHotKey Classes.")]
            public bool Import { get; set; }

            [Option('x', "export", HelpText = "If true then export will be included for AutoHotKey Classes.")]
            public bool Export { get; set; }

            [Option('w', "whitespace", Required = false, HelpText = "Ignore whites space when reading xml. Requires import to be true. Applies only to AHK")]
            public bool XmlIgnoreWhiteSpace { get; set; }


            // by using enum in this manner the user can input -l or --language for the language code and not be as limited
            public LanguageOption Lang
            {
                get
                {
                    if (this.m_LangSet == false)
                    {
                        this.m_LangSet = true;
                        if (string.IsNullOrEmpty(this.Language))
                        {
                            this.m_lang = LanguageOption.NONE;
                            return this.m_lang;
                        }
                        string sLang = this.GetCleanedCommand(this.Language, 2, 3);
                        if (string.IsNullOrEmpty(sLang))
                        {
                            this.m_lang = LanguageOption.NONE;
                            return this.m_lang;
                        }
                        sLang = sLang.ToLower();
                        if (sLang == "cs")
                        {
                            this.m_lang = LanguageOption.CSHARP;
                        }
                        else if (sLang == "ahk")
                        {
                            this.m_lang = LanguageOption.AUTOHOTKEY;
                        }

                    }
                    return this.m_lang;
                }
            }

            // clean up the user input if needed. will ignore all chars other than a-z
            // and ignores case
            // and only matches lengths between MinChars and MaxChars
            private string GetCleanedCommand(string InputText, int MinChars, int MaxChars)
            {
                    Regex regex = new Regex(
                      "([a-zA-Z]{" + MinChars.ToString() + "," + MaxChars.ToString() + "})",
                    RegexOptions.Singleline
                    | RegexOptions.CultureInvariant
                    | RegexOptions.Compiled
                    );
                MatchCollection ms = regex.Matches(InputText);

                string retval = string.Empty;
                if (ms.Count == 1)
                {
                    retval = ms[0].Captures[0].Value;
                }
                return retval;
            }





            //
            // Marking a property of type IParserState with ParserStateAttribute allows you to
            // receive an instance of ParserState (that contains a IList<ParsingError>).
            // This is equivalent from inheriting from CommandLineOptionsBase (of previous versions)
            // with the advantage to not propagating a type of the library.
            //
            [ParserState]
            public IParserState LastParserState { get; set; }

            [HelpOption]
            public string GetUsage()
            {
                return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
            }
        }

        
    }
   
}
