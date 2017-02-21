using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.Schema;
using System.CodeDom;
using System.CodeDom.Compiler;
using Microsoft.CSharp;

// Original Source: http://mikehadlow.blogspot.ca/2007/01/writing-your-own-xsdexe.html
namespace BigByteTechnologies.XsdOut
{
    public class XsdToCS
    {
        // Test for XmlSchemaImporter
        private string m_Code = string.Empty;
        private string m_FileName = string.Empty;

        private List<string> m_Warnings;

        public List<string> Warnings
        {
            get { return m_Warnings; }
        }

        private List<string> m_Errors;

        public List<string> Errors
        {
            get { return m_Errors; }
        }

        public bool RemoveExtraAttributes { get; set; }
        public string XsdFileName { get; set; }
        public string TargetNamesapce { get; set; }
       
        public bool HasWarnings
        {
            get { return this.m_Warnings.Count > 0; }
        }

        public bool HasErrors
        {
            get { return this.m_Errors.Count > 0; }
        }


        public string Code
        {
            get { return m_Code; }
        }
        public XsdToCS(string xsdFileName) : this(xsdFileName, string.Empty)
        {
        }

        public  XsdToCS(string xsdFileName, string TargetNs)
        {
            this.RemoveExtraAttributes = false;
            this.m_Warnings = new List<string>();
            this.m_Errors = new List<string>();
            this.TargetNamesapce = TargetNs;
            this.XsdFileName = xsdFileName;
   
         
        
        }

        public bool Convert()
        {
            this.m_Errors.Clear();
            this.m_Warnings.Clear();
            // identify the path to the xsd
            if (string.IsNullOrEmpty(this.XsdFileName))
            {
                this.Errors.Add(Properties.Resources.ErrorEmptyXsdFileName);
                return false;
            }
            string _xsdFileName = string.Empty;
            if (this.XsdFileName.IndexOf(Path.DirectorySeparatorChar) < 0)
            {
                string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                _xsdFileName = Path.Combine(path, this.XsdFileName);
            }
            else
            {
                _xsdFileName = this.XsdFileName;
            }

            if (File.Exists(_xsdFileName) == false)
            {
                this.Errors.Add(Properties.Resources.ErrorMissingXsdFile);
                return false;
            }
            this.m_FileName = _xsdFileName;

            // load the xsd

            XmlSchema xsd;
            using (FileStream stream = new FileStream(this.m_FileName, FileMode.Open, FileAccess.Read))
            {
                xsd = XmlSchema.Read(stream, null);
                ValidationEventHandler vc = new ValidationEventHandler(ValidationCallback);
            }
            //Console.WriteLine("xsd.IsCompiled {0}", xsd.IsCompiled);

            XmlSchemas xsds = new XmlSchemas();
            ValidationEventHandler vHandler = new ValidationEventHandler(ValidationCallback);
            xsds.Add(xsd);
            xsds.Compile(vHandler, true);
            XmlSchemaImporter schemaImporter = new XmlSchemaImporter(xsds);

            // create the codedom
            CodeNamespace codeNamespace = new CodeNamespace();
            XmlCodeExporter codeExporter = new XmlCodeExporter(codeNamespace);

            List<XmlTypeMapping> maps = new List<XmlTypeMapping>();
            foreach (XmlSchemaType schemaType in xsd.SchemaTypes.Values)
            {
                maps.Add(schemaImporter.ImportSchemaType(schemaType.QualifiedName));
            }
            foreach (XmlSchemaElement schemaElement in xsd.Elements.Values)
            {
                maps.Add(schemaImporter.ImportTypeMapping(schemaElement.QualifiedName));
            }
            //foreach(XmlSchemaAttribute schemaAttrib in xsd.Attributes.Values)
            //{
            //    maps.Add(schemaImporter.imp)
            //}
            foreach (XmlTypeMapping map in maps)
            {
                codeExporter.ExportTypeMapping(map);
            }
            if (RemoveExtraAttributes == true)
            {
                RemoveAttributes(codeNamespace);
            }
            

            // Check for invalid characters in identifiers
            CodeGenerator.ValidateIdentifiers(codeNamespace);

            // output the C# code
            CSharpCodeProvider codeProvider = new CSharpCodeProvider();

            this.m_Code = string.Format(Properties.Resources.AutoGenMessage, Assembly.GetExecutingAssembly().GetName().Version.ToString());
            this.m_Code += Environment.NewLine;

            using (StringWriter writer = new StringWriter())
            {
                codeProvider.GenerateCodeFromNamespace(codeNamespace, writer, new CodeGeneratorOptions());
                //Console.WriteLine(writer.GetStringBuilder().ToString());
                this.m_Code += writer.GetStringBuilder().ToString();
            }
            return this.HasErrors;
        }

        // Remove all the attributes from each type in the CodeNamespace, except
        // System.Xml.Serialization.XmlTypeAttribute
        private void RemoveAttributes(CodeNamespace codeNamespace)
        {
            foreach (CodeTypeDeclaration codeType in codeNamespace.Types)
            {
                CodeAttributeDeclaration xmlTypeAttribute = null;
                foreach (CodeAttributeDeclaration codeAttribute in codeType.CustomAttributes)
                {
                    //Console.WriteLine(codeAttribute.Name);
                    if (codeAttribute.Name == "System.Xml.Serialization.XmlTypeAttribute")
                    {
                        xmlTypeAttribute = codeAttribute;
                    }
                }
                codeType.CustomAttributes.Clear();
                if (xmlTypeAttribute != null)
                {
                    codeType.CustomAttributes.Add(xmlTypeAttribute);
                }
            }
        }

        private void ValidationCallback(object sender, ValidationEventArgs args)
        {
            if (args.Severity == XmlSeverityType.Warning)
                this.Warnings.Add(string.Format("Warning:'{0}'", args.Message));
            else if (args.Severity == XmlSeverityType.Error)
                this.Warnings.Add(string.Format("Warning:'{0}'", args.Message));
        }


    }
    public static class AssemblyBuilder
    {
        public static Assembly BuildAssembly(string code)
        {
            // http://www.codeproject.com/Articles/9019/Compiling-and-Executing-Code-at-Runtime
            CSharpCodeProvider provider = new CSharpCodeProvider();

            //ICodeCompiler compiler = provider.CreateCompiler();
            CompilerParameters compilerparams = new CompilerParameters();
            compilerparams.GenerateExecutable = false;
            compilerparams.GenerateInMemory = true;
            compilerparams.ReferencedAssemblies.Add("System.dll");
            compilerparams.ReferencedAssemblies.Add("System.Xml.dll");
            CompilerResults results = provider.CompileAssemblyFromSource(compilerparams, code);
            if (results.Errors.HasErrors)
            {
                StringBuilder errors = new StringBuilder("Compiler Errors :\r\n");
                foreach (CompilerError error in results.Errors)
                {
                    errors.AppendFormat("Line {0},{1}\t: {2}\n",
                           error.Line, error.Column, error.ErrorText);
                }
                throw new Exception(errors.ToString());
            }
            else
            {
                return results.CompiledAssembly;
            }
        }
    }
}