using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.CodeDom.Compiler;
using System.Reflection;
using BigByteTechnologies.XsdOut;
using System.Xml.Serialization;
using System.Xml.Schema;

namespace BigByteTechnologies.XsdOut.AutoHotkey
{
    internal class AutoHotkeyBuilder
    {
        #region Fields/Members
        System.IO.StringWriter baseTextWriter;
        IndentedTextWriter w;
        private XsdToCS m_XsdCs;
        private const char q =  '\u0022';
        private const char bo = '{';
        private const char bc = '}';
        private const string tc = "\" `t`r`n\"";
        Assembly m_Asm;
        Dictionary<string, SpecialPropertyDescriptor> m_SpDic;
        List<XmlTypeMapping> maps;
        private string m_FileName;
        #endregion

        #region Public Properties
        public bool Import { get; set; }
        public bool Export { get; set; }
        public bool IncludeXmlHelper { get; set; }

        public bool XmlIgnoreWhiteSpace { get; set; }
        #endregion

        #region Constructor
        public AutoHotkeyBuilder(string xsdFile) : this(xsdFile,string.Empty)
        {
        }

        public AutoHotkeyBuilder(string xsdFile, string TargetNs)
        {
            this.m_FileName = xsdFile;
            m_SpDic = new Dictionary<string, SpecialPropertyDescriptor>();
            this.Import = true;
            this.Export = true;
            baseTextWriter = new System.IO.StringWriter();
            w = new IndentedTextWriter(baseTextWriter);
            this.m_XsdCs = new XsdToCS(xsdFile, TargetNs);
            this.m_XsdCs.Convert();
            this.m_Asm = AssemblyBuilder.BuildAssembly(this.m_XsdCs.Code);
            this.maps = new List<XmlTypeMapping>();
            this.PopulateMap();
        }
        #endregion

        private void PopulateMap()
        {
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

            this.maps.Clear();
            //foreach (XmlSchemaType schemaType in xsd.SchemaTypes.Values)
            //{
            //    maps.Add(schemaImporter.ImportSchemaType(schemaType.QualifiedName));
            //}
            foreach (XmlSchemaElement schemaElement in xsd.Elements.Values)
            {
                maps.Add(schemaImporter.ImportTypeMapping(schemaElement.QualifiedName));
            }

        }

        private void ValidationCallback(object sender, ValidationEventArgs args)
        {
           
        }

        #region Build
        public void Build()
        {
            m_SpDic.Clear();

            var types = this.m_Asm.GetTypes();
            if (types.Count() > 0)
            {
                w.Indent = 0;
                
                w.WriteLine(Properties.Resources.AutoGenMessage, Assembly.GetExecutingAssembly().GetName().Version.ToString());
                w.WriteLine();
            }

            #region Map Class and Elements
            // when using import or export there will need to be some mapping to help write the import and export methods
            // each class in the assemble does not have a direct mapping back to its parent class so this mapping creats a
            // crude way of mapping the class names that are different then the element names back to the element name.
            // this is needed for xpath query's and XML export.
            // Not all class names are mapped properly but it turns out the class name that are not mapped correctly actually
            // are the same as the element names. 
            //
            // If array has element of XmlArrayItemAttribute and element of XmlArrayAttribute
            // then key name should be Class Name + Uppercase First ElementName
            //
            // If array only has XmlArrayItemAttribute attribute then the
            // key name should simply be the ElementName
            // The issue is this program does not generate the XmlArrayAttribute on the class properties but XSD.exe tool does.
            if ((this.Export == true) || (this.Import == true))
            {
                foreach (var type in types)
                {
                    if (type.IsClass)
                    {
                        PropertyInfo[] props = type.GetProperties(BindingFlags.Public |
                                                 BindingFlags.NonPublic |
                                                 BindingFlags.Instance); // Obtain all fields
                        foreach (var prop in props)
                        {

                            if ((prop.PropertyType.IsPrimitive == false) && (prop.PropertyType != typeof(string)))
                            {
                                // some needed mapping or property to class do not have any attributes.
                                // just in case will do blanket coverage
                                SpecialPropertyDescriptor spd = new SpecialPropertyDescriptor();
                                spd.ElementName = prop.Name;
                                spd.Key = type.Name + Helper.UpperCaseFirst(spd.ElementName);
                                spd.ClassName = type.Name;
                                spd.IsArray = prop.PropertyType.IsArray;
                                spd.Property = prop.Name;
                                spd.PropertyType = prop.PropertyType.ToString();

                                if (this.m_SpDic.ContainsKey(spd.Key) == false)
                                {
                                    this.m_SpDic.Add(spd.Key, spd);
                                }

                                var attribs = prop.GetCustomAttributes(false);
                                if (attribs == null)
                                {
                                    continue;
                                }
                                if (prop.PropertyType.IsArray)
                                {
                                    bool bHasItemAttrib = false;
                                    bool HasArrayAttrib = false;

                                    foreach (var attrib in attribs)
                                    {
                                        Type attribType = attrib.GetType();
                                        if (attribType == typeof(System.Xml.Serialization.XmlArrayItemAttribute))
                                        {
                                            bHasItemAttrib = true;
                                        }
                                        else if (attribType == typeof(System.Xml.Serialization.XmlArrayAttribute))
                                        {
                                            HasArrayAttrib = true;
                                        }
                                    }


                                    foreach (var attrib in attribs)
                                    {

                                        Type attribType = attrib.GetType();

                                        if (attribType == typeof(System.Xml.Serialization.XmlArrayItemAttribute))
                                        {
                                            var xRay = (System.Xml.Serialization.XmlArrayItemAttribute)attrib;
                                            SpecialPropertyDescriptor sp = new SpecialPropertyDescriptor();
                                            if (string.IsNullOrEmpty(xRay.ElementName))
                                            {
                                                sp.ElementName = prop.Name;
                                            }
                                            else
                                            {
                                                sp.ElementName = xRay.ElementName;
                                            }

                                            if ((HasArrayAttrib == true) && (bHasItemAttrib == true))
                                            {
                                                sp.Key = type.Name + Helper.UpperCaseFirst(sp.ElementName);
                                            }
                                            else if (bHasItemAttrib == true)
                                            {

                                                // it seems that the CS code generation of this app does not include
                                                // the XmlArrayAttribute  so
                                                // if ((HasArrayAttrib == true) && (bHasItemAttrib == true))
                                                // will never be true therefore ignoring
                                                //sp.Key = sp.ElementName;
                                                sp.Key = type.Name + Helper.UpperCaseFirst(sp.ElementName);
                                            }

                                            sp.ClassName = type.Name;
                                            sp.IsArray = true;
                                            sp.Property = prop.Name;
                                            sp.PropertyType = prop.PropertyType.ToString();

                                            if (this.m_SpDic.ContainsKey(sp.Key) == false)
                                            {
                                                this.m_SpDic.Add(sp.Key, sp);
                                            }
                                        }
                                    }

                                }
                                else
                                {
                                    foreach (var attrib in attribs)
                                    {
                                        if (attrib.GetType() == typeof(System.Xml.Serialization.XmlElementAttribute))
                                        {
                                            var xRay = (System.Xml.Serialization.XmlElementAttribute)attrib;
                                            SpecialPropertyDescriptor sp = new SpecialPropertyDescriptor();
                                            if (string.IsNullOrEmpty(xRay.ElementName))
                                            {
                                                sp.ElementName = prop.Name;
                                            }
                                            else
                                            {
                                                sp.ElementName = xRay.ElementName;
                                            }
                                            sp.Key = type.Name + Helper.UpperCaseFirst(sp.ElementName);
                                            sp.ClassName = type.Name;
                                            sp.IsArray = false;
                                            sp.Property = prop.Name;
                                            sp.PropertyType = prop.PropertyType.ToString();
                                            if (this.m_SpDic.ContainsKey(sp.Key) == false)
                                            {
                                                this.m_SpDic.Add(sp.Key, sp);
                                            }
                                            break;
                                        }
                                    }
                                }

                            }
                        }
                    }
                }
            }

            //foreach (var sp in this.m_SpDic)
            //{
            //    Console.WriteLine("{0}, Class:{1}, Array:{2}", sp.Value.Key, sp.Value.ClassName, sp.Value.IsArray.ToString());
            //    Console.WriteLine("Property:{0}, Element:{1}, Type:{2}", sp.Value.Property, sp.Value.ElementName, sp.Value.PropertyType);

            //    Console.WriteLine();
            //}
            //Console.WriteLine("press any key to continue");
            //Console.ReadLine();
            //Environment.Exit(0);
            #endregion

            foreach (var type in types)
            {

                if (type.IsEnum)
                {
                    WriteEnum(type);

                }
                if (type.IsClass)
                {
                    WriteClass(type);
                }

            }
            if (this.IncludeXmlHelper == true)
            {
                w.WriteLine();
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = @"BigByteTechnologies.XsdOut.AHK.XmlHelper.ahk";

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                using (StreamReader reader = new StreamReader(stream))
                {
                    string result = reader.ReadToEnd();
                    w.Write(result);
                }
            }
        }
        #endregion

        #region Class
        private void WriteClass(Type t)
        {

            Dictionary<string, FieldInfo> fDic = new Dictionary<string, FieldInfo>();
            string ClassName = t.Name;
            Type[] emptyArgumentTypes = Type.EmptyTypes;
            ConstructorInfo ctor = t.GetConstructor(emptyArgumentTypes);
            if (ctor == null)
            {
                return;
            }
            object csClass = ctor.Invoke(new object[] { });

            w.Indent = 0;
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteClass()");
#endif
            #region Open Class
            w.WriteLine(@"class {0} extends MfObject", ClassName);
            w.WriteLine(@"{");
            #endregion

            #region Fields
            w.Indent++;
            FieldInfo[] fields = t.GetFields(BindingFlags.Public |
                                              BindingFlags.NonPublic |
                                              BindingFlags.Instance); // Obtain all fields
            foreach (var field in fields) // Loop through fields
            {
                var objF = csClass.GetType().GetField(field.Name, BindingFlags.Public |
                                                              BindingFlags.NonPublic |
                                                              BindingFlags.Instance);

                string name = field.Name; // Get string name
                if (fDic.ContainsKey(name))
                {
                    continue;
                }
                fDic.Add(name, field);
                w.Write(name);
                if (objF == null )
                {
                    w.WriteLine(" := MfNull.Null");
                }
                else
                {
                    if (objF.FieldType == typeof(bool))
                    {
                        w.WriteLine(" := false");
                    }
                    else if (objF.FieldType == typeof(int))
                    {
                        w.WriteLine(" := 0");
                    }
                    else if (objF.FieldType == typeof(string))
                    {
                        w.WriteLine(" := {0}{0}", q);
                    }
                    else
                    {
                        w.WriteLine(" := MfNull.Null");
                    }
                }
               

                
                var eType = field.FieldType.GetElementType();
                //if (eType == null)
                //{
                //    Console.WriteLine("Element type is null.");
                //}
                //else
                //{
                //    Console.WriteLine("Element Type:{0}", eType.ToString());
                //}
                //Console.WriteLine();
                
                
              
            }
            w.WriteLine();

            #endregion

            #region Constructor
            w.WriteLine();

            WriteConstructor(t, ref csClass, ref fDic);
            #endregion

            #region ParseXml
            if (this.Import == true)
            {
                this.WriteParseXml(t, ref csClass, ref fDic);
            }
            #endregion

            #region ToXml
            if (this.Export == true)
            {
                this.WriteToXml(t, ref csClass, ref fDic);
            }
            #endregion

            #region Properties
            PropertyInfo[] props = t.GetProperties(BindingFlags.Public |
                                              BindingFlags.NonPublic |
                                              BindingFlags.Instance); // Obtain all fields
            foreach (var prop in props)
            {
                if (string.IsNullOrWhiteSpace(prop.Name))
                {
                    continue;
                }
                string FieldKey = prop.Name + "Field";
                
                if (!fDic.ContainsKey(FieldKey))
                {
                    if (prop.Name.EndsWith(@"Specified"))
                    {
                        FieldKey = prop.Name.Substring(0, prop.Name.Length - 9) +  "FieldSpecified";
                    }
                    //FieldKey = prop.Name + "Specified" + "Field";
                }
                if (!fDic.ContainsKey(FieldKey))
                {
                    // sometime the field convert the first letter of the property to upper
                    // such as wihen the property name is value. Convert first char of
                    // property name back to lower and test the key again
                    FieldKey = char.ToLower(prop.Name[0]) + prop.Name.Substring(1);
                    FieldKey += "Field";
                }
                if (!fDic.ContainsKey(FieldKey))
                {
                    continue;
                }
                //FieldInfo PropField = fDic[FieldKey];

                w.Write(prop.Name);
                w.WriteLine(@"[]");
                w.WriteLine(@"{"); // Open Properyt
                w.Indent++; // in get
                w.WriteLine("get");
                w.WriteLine(@"{"); // Open get
                w.Indent++; 
                w.Write(@"return this.");
                w.WriteLine(FieldKey);
                w.Indent--;
                w.WriteLine(@"}"); // close get
                //w.Indent--;
                w.WriteLine(@"set");
                w.WriteLine(@"{"); // open set
                w.Indent++;
                w.Write(@"this.");
                w.Write(FieldKey);
                w.WriteLine(@" := value");
                w.Write(@"return this.");
                w.WriteLine(FieldKey);
                w.Indent--;
                w.WriteLine(@"}"); // close set
                w.Indent--;
                w.WriteLine(@"}"); // close property
                w.WriteLine();

            }
            #endregion

            #region GetType
            /*
             * Inherites From MfObject
            w.WriteLine("GetType()");
            w.WriteLine("{");

            w.Indent++;
            w.WriteLine("return base.GetType()");
            w.Indent--;

            w.WriteLine("}");
            w.WriteLine();
            */
            #endregion

            #region IS
            /* 
             * Inherits from MfObject
            w.WriteLine(@"Is(type)");
            w.WriteLine(@"{");

            w.Indent++;
            w.WriteLine(@"typeName := null");
            w.WriteLine(@"if (IsObject(type))");
            w.WriteLine(@"{");
            w.Indent++;
            w.WriteLine(@"if (MfObject.IsObjInstance(type, {0}MfType{0}))", q);
            w.WriteLine(@"{");

            w.Indent++;
            w.WriteLine(@"typeName := type.ClassName");
            w.Indent--;
            w.WriteLine(@"}");
            w.WriteLine(@"else if (type.__Class)");
            w.WriteLine(@"{");
            w.Indent++;
            w.WriteLine(@"typeName := type.__Class");
            w.Indent--;
            w.WriteLine(@"}");
            w.WriteLine(@"else if (type.base.__Class)");
            w.WriteLine(@"{");
            w.Indent++;
            w.WriteLine(@"typeName := type.base.__Class");
            w.Indent--;
            w.WriteLine("}");

            w.Indent--;
            w.WriteLine(@"}");
            w.WriteLine(@"else if (type ~= {0}^[a-zA-Z0-9.]+${0})", q);
            w.WriteLine(@"{");
            w.Indent++;
            w.WriteLine(@"typeName := type");
            w.Indent--;
            w.WriteLine("}");

            w.WriteLine(@"if (typeName = {0}{1}{0})", q, t.Name);
            w.WriteLine(@"{");
            w.Indent++;
            w.WriteLine(@"return true");
            w.Indent--;
            w.WriteLine("}");
            w.WriteLine(@"return base.Is(type)");

            w.Indent--;
            w.WriteLine(@"}");
            w.WriteLine();

            #endregion

            #region IsObjInstance
            w.WriteLine(@"IsObjInstance(obj, objType = {0}{0})", q);
            w.WriteLine(@"{");
            w.Indent++;
            w.WriteLine(@"return MfObject.IsObjInstance(obj, objType)");
            w.Indent--;
            w.WriteLine(@"}");
            w.WriteLine();
            */
            #endregion

            #region ToString
            /*
             * Inherits from MfObject
            w.WriteLine(@"ToString()");
            w.WriteLine(@"{");
            w.Indent++;
            w.WriteLine(@"return base.ToString()");
            w.Indent--;
            w.WriteLine(@"}");
            w.WriteLine();
            */
            #endregion

            #region Enumerator
            if ((fields.Length == 1) && (props.Length > 0) && (fields[0].FieldType.IsArray == true))
            {
                // one field and that field is an array. lets add an enumerator
                // need the property name that matches the field name.
                // only proceed if property name and field name can be matched
                var fType = fields[0];
                var pType = props[0];
                if (fType.FieldType.Name == pType.PropertyType.Name)
                {
                    WriteEnumerator(ClassName, fType.FieldType, pType);
                }
            }
            #endregion

            #region Close Class
            w.Indent = 0;
            w.WriteLine("}");
            w.WriteLine();
            #endregion
#if DEBUG
            w.WriteLineNoTabs("; End : WriteClass()");
#endif
        }

        #region AutoHotkey Enumerator
        private void WriteEnumerator(string ClassName, Type t, PropertyInfo pi)
        {
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteEnumerator()");
#endif
            Type ElementType = t.GetElementType(); // get the type of element
            if (string.Equals(ElementType.Name, "object", StringComparison.CurrentCultureIgnoreCase))
            {
                return;
            }
            /*
             * GetEnumerator is not needed it is already inherited and does not require overriding
            w.WriteLine(@"GetEnumerator()");
            w.WriteLine(bo);
            w.Indent++;
            w.WriteLine(@"return this._NewEnum()");
            w.Indent--;
            w.WriteLine(bc);
            */

            w.WriteLine();
            w.Write(@"_NewEnum() ");
            w.WriteLine(bo);
            w.Indent++;
            w.Write(@"return new ");
            w.Write(ClassName);
            w.WriteLine(@".Enumerator(this)");
            w.Indent--;
            w.WriteLine(bc);

            w.WriteLine();
            w.WriteLine(@"; Internal Class");
            w.WriteLine(@"class Enumerator");
            w.WriteLine(bo);
            w.Indent++;
            w.WriteLine(@"m_Parent := Null");
            w.WriteLine(@"m_KeyEnum := Null");
            w.WriteLine(@"m_index := 0");
            w.WriteLine(@"m_count := 0");
            w.WriteLine();

            w.Write(@"__new(ParentClass) ");
            w.WriteLine(bo);
            w.Indent++;
            w.WriteLine(@"this.m_Parent := ParentClass");
            w.Write(@"this.m_count := this.m_Parent.");
            w.Write(pi.Name);
            w.WriteLine(@".Count");
            w.Indent--;
            w.WriteLine(bc);

            w.WriteLine();
            w.WriteLine(@"Next(ByRef key, ByRef value)");
            w.WriteLine(bo);
            w.Indent++;
            w.WriteLine(@"if (this.m_index < this.m_count)");
            w.WriteLine(bo);
            w.Indent++;
            w.WriteLine(@"key := this.m_index");
            w.Write(@"value := this.m_Parent.");
            w.Write(pi.Name);
            w.WriteLine(@".Item[key]");
            w.Indent--;
            w.WriteLine(bc);

            w.WriteLine(@"this.m_index++");
            w.WriteLine(@"if (this.m_index > this.m_count)");
            w.WriteLine(bo);
            w.Indent++;
            w.WriteLine(@"return 0");
            w.Indent--;
            w.WriteLine(bc);
            w.WriteLine(@"else");
            w.WriteLine(bo);
            w.Indent++;
            w.WriteLine(@"return true");
            w.Indent--;
            w.WriteLine(bc);

            w.Indent--;
            w.WriteLine(bc);
            
           
            w.Indent--;
            w.WriteLine(bc);
#if DEBUG
            w.WriteLineNoTabs("; End : WriteEnumerator()");
#endif
        }
        #endregion

        private void WriteConstructor(Type t, ref object csClass, ref Dictionary<string, FieldInfo> fDic)
        {
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteConstructor()");
#endif
            if (t.IsClass == false)
            {
                return;
            }

            if (this.Import == true)
            {
                w.Write(@"__New(args*) ");
            } 
            else
            {
                w.Write(@"__New()");
            }
           
            w.WriteLine(@"{");
            w.Indent++;
            w.WriteLine(@"base.__New()");
            PropertyInfo[] props = t.GetProperties(BindingFlags.Public |
                                              BindingFlags.NonPublic |
                                              BindingFlags.Instance); // Obtain all fields
          
            foreach (var prop in props)
            {
                if (string.IsNullOrWhiteSpace(prop.Name))
                {
                    continue;
                }
                string FieldKey = prop.Name + "Field";

                if (!fDic.ContainsKey(FieldKey))
                {
                    if (prop.Name.EndsWith(@"Specified"))
                    {
                        FieldKey = prop.Name.Substring(0, prop.Name.Length - 9) + "FieldSpecified";
                    }
                    //FieldKey = prop.Name + "Specified" + "Field";
                }
                if (!fDic.ContainsKey(FieldKey))
                {
                    // sometime the field convert the first letter of the property to upper
                    // such as wihen the property name is value. Convert first char of
                    // property name back to lower and test the key again
                    FieldKey = char.ToLower(prop.Name[0]) + prop.Name.Substring(1);
                    FieldKey += "Field";
                }
                if (!fDic.ContainsKey(FieldKey))
                {
                    continue;
                }
                TypeCode tc = Type.GetTypeCode(prop.PropertyType);
               

                string pName = prop.Name;
               
                var pInfo = csClass.GetType().GetProperty(pName);
                if (pInfo == null)
                {
                    Console.WriteLine("Null Property value:{0} for type:{1}", pName, t.Name);
                    continue;
                }

                var value = pInfo.GetValue(csClass, null);
                if (prop.PropertyType.IsArray)
                {
                    Type underLye = prop.PropertyType.GetElementType();
                    bool isEnum = false;
                    if (underLye != null && underLye.IsEnum == true)
                    {
                        isEnum = true;
                    }

                    string ppType = prop.PropertyType.ToString();
                    w.Write(@"this.");
                    w.Write(FieldKey);
                    w.Write(@" := ");
                    if (ppType == "System.Object[]")
                    {
                        w.Write(" new MfCollection(");
                    }
                   
                    else
                    {
                        w.Write(" new MfGenericList(");
                        if (isEnum == true)
                        {
                            w.Write(@"MfEnum.EnumItem");
                        } 
                        else
                        {
                            w.Write(prop.PropertyType.Name.Substring(0, prop.PropertyType.Name.Length - 2));
                        }
                       
                    }
                    w.WriteLine(@")");
                    
                    continue;
                }
              
                if (value == null && tc != TypeCode.Object)
                {
                    //Console.Write(prop.PropertyType.ToString());
                    //Console.WriteLine(" Is Null");
                    continue;
                }
                if (prop.PropertyType.IsEnum)
                {
                    string valueType = value.GetType().ToString();

                    var att = prop.GetCustomAttributes(true);
                    bool EnumHasFlags = false;
                    if (att != null)
                    {
                        foreach (var attr in att)
                        {
                            Type attrType = attr.GetType();
                            if (attrType == typeof(System.FlagsAttribute))
                            {
                                EnumHasFlags = true;
                                break;
                            }
                        }
                    }
                    if (EnumHasFlags == true)
                    {
                        w.Write(@"this.");
                        w.Write(FieldKey);
                        w.Write(" := new ");
                        w.Write(valueType);
                        w.Write(@"(");
                        w.Write(valueType);
                        w.Write(".Instance.");
                        w.Write(value.ToString());
                        w.WriteLine(@".Value)");

                    }
                    else
                    {
                        w.Write(@"this.");
                        w.Write(FieldKey);
                        w.Write(" := ");
                        w.Write(valueType);
                        w.Write(".Instance.");
                        w.WriteLine(value.ToString());
                    }
                   
                    //w.WriteLine(@"; EnumValue Here - Field:{0}, Property:{1}, typeof:{2}", FieldKey, prop.Name, value.GetType().ToString());
                    continue;

                }

                switch (tc)
                {
                    
                    case TypeCode.Object:
                        w.Write(@"this.");
                        w.Write(FieldKey);
                        w.Write(@" := new ");
                        w.Write(prop.PropertyType.ToString());
                        w.WriteLine(@"()");
                        continue;
                    case TypeCode.Boolean:
                        w.Write(@"this.");
                        w.Write(FieldKey);
                        w.Write(@" := ");
                        w.WriteLine(value.ToString().ToLower());
                        continue;
                    case TypeCode.SByte:
                    case TypeCode.Byte:
                    case TypeCode.Int16:
                    case TypeCode.UInt16:
                    case TypeCode.Int32:
                    case TypeCode.UInt32:
                    case TypeCode.Int64:
                    case TypeCode.UInt64:
                    case TypeCode.Single:
                    case TypeCode.Double:
                    case TypeCode.Decimal:
                        w.Write(@"this.");
                        w.Write(FieldKey);
                        w.Write(@" := ");
                        w.WriteLine(value.ToString());
                        continue;
                    case TypeCode.DateTime:
                    case TypeCode.Char:
                    case TypeCode.String:
                        w.Write(@"this.");
                        w.Write(FieldKey);
                        w.Write(@" := ");
                        w.WriteLine(@"{0}{1}{0}", q, (string)value);
                        continue;
                    case TypeCode.DBNull:
                    case TypeCode.Empty:
                    default:
                        continue;
                }
                               
            }
            w.Write(@"this.m_isInherited := this.base.__Class != {0}", q);
            w.Write(t.Name);
            w.WriteLine(q);
            w.WriteLine();

            if (this.Import == true)
            {
                w.WriteLine(@"pCount := 0");
                w.WriteLine(@"for i, param in args");
                w.WriteLine(bo);
                w.Indent++;
                w.WriteLine(@"pCount ++");
                w.Indent--;
                w.WriteLine(bc);

                w.WriteLine(@"if (pCount = 1)");
                w.WriteLine(bo);
                w.Indent++;
                w.WriteLine(@"this.ParseXml(args[1])");
                w.Indent--;
                w.WriteLine(bc);
                w.WriteLine();

            }


            w.Indent--;
            w.WriteLine(@"}");
            w.WriteLine();
#if DEBUG
            w.WriteLineNoTabs("; End : WriteConstructor()");
#endif
        }

        #region Export Xml
        private void WriteToXml(Type t, ref object csClass, ref Dictionary<string, FieldInfo> fDic)
        {
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteToXml()");
#endif
            if (t.IsClass == false)
            {
                return;
            }
            string ElementName;
            if (this.m_SpDic.ContainsKey(t.Name))
            {
                var info = this.m_SpDic[t.Name];
                ElementName = info.ElementName;
            }
            else
            {
                ElementName = t.Name;
            }
            w.Write(@"ToXml() ");
            w.WriteLine(bo);
            w.Indent++;
            w.WriteLine(@"this.VerifyIsInstance(this, A_LineFile, A_LineNumber, A_ThisFunc)");
            w.WriteLine(@"retval := Null");

            w.WriteLine(@"try");
            w.WriteLine(bo);
            w.Indent++;
            w.WriteLine(@"smfXml := new MfString()");
            w.Write(@"smfXml.Append({0}<", q); // open xml
            w.Write(ElementName);
            w.WriteLine(@"{0})", q);
            WriteToXmlAttributes(t, ref fDic);
            w.WriteLine(@"smfXml.AppendLine({0}>{0})", q);
            // write properties
            WriteToXmlProperties(t, ref csClass, ref fDic);

            w.Write(@"smfXml.AppendLine({0}</", q);
            w.Write(ElementName);
            w.WriteLine(@">{0})", q); // close xml
            w.WriteLine(@"retval := smfXml.Value");
            w.Indent--;
            w.WriteLine(bc);
            w.WriteLine(@"catch e");
            w.WriteLine(bo);
            w.Indent++;
            w.WriteLine(@"ex := new MfException(MfEnvironment.Instance.GetResourceString({0}Exception_Error{0}, A_ThisFunc), e)", q);
            w.WriteLine(@"ex.SetProp(A_LineFile, A_LineNumber, A_ThisFunc)");
            w.WriteLine(@"throw ex");
            w.Indent--;
            w.WriteLine(bc);

            w.WriteLine(@"return retval");
            w.Indent--;
            w.WriteLine(bc);
            w.WriteLine();
#if DEBUG
            w.WriteLineNoTabs("; End: WriteToXml()");
#endif
        }

        private void WriteToXmlAttributes(Type t, ref Dictionary<string, FieldInfo> fDic)
        {
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteToXmlAttributes()");
#endif
            PropertyInfo[] props = t.GetProperties(BindingFlags.Public |
                                             BindingFlags.NonPublic |
                                             BindingFlags.Instance); // Obtain all fields
            Dictionary<string, PropertyInfo> fp = new Dictionary<string, PropertyInfo>();

            foreach (var prop in props)
            {
                var pt = prop.PropertyType;


                var att = prop.GetCustomAttributes(typeof(System.Xml.Serialization.XmlAttributeAttribute), true);
                if (att.Count() > 0)
                {
                    string FieldKey = this.GetFieldKey(prop, ref fDic);
                    if (string.IsNullOrEmpty(FieldKey))
                    {
                        continue;
                    }
                    fp.Add(FieldKey, prop);
                }

            }

            
            foreach (var fkey in fp.Keys)
            {
                PropertyInfo pi = fp[fkey];
                string f = Helper.UpperCaseFirst(fkey);
                Type pt = pi.PropertyType;
                if (pt.IsEnum)
                {
                    w.Write(@"str");
                    w.Write(f);
                    w.Write(@" := this.");
                    w.Write(fkey);
                    w.WriteLine(@".ToString()");


                    w.Write(@"smfXml.Append(MfString.Format({0} ", q);
                    w.Write(pi.Name);
                    w.Write(@"={0}{1}{0}");
                    w.Write("{0}, {0}{0}{0}{0}, ", q);
                    w.Write(@"str");
                    w.Write(f);
                    w.WriteLine(@"))");
                    w.WriteLine();

                }
                else if (pt == typeof(bool))
                {
                    w.Write(@"mfb");
                    w.Write(f);
                    w.Write(@" := new MfBool(this.");
                    w.Write(fkey);
                    w.WriteLine(@")");

                    w.Write(@"smfXml.Append(MfString.Format({0} ", q);
                    w.Write(pi.Name);
                    w.Write(@"={0}{1}{0}");
                    w.Write("{0}, {0}{0}{0}{0}, mfb", q);
                    w.Write(f);
                    w.WriteLine(@".ToString()))");
                    w.WriteLine();

                }
                else if (pt == typeof(int))
                {
                    w.Write(@"mfi");
                    w.Write(f);
                    w.Write(@" := new MfInteger(this.");
                    w.Write(fkey);
                    w.WriteLine(@")");

                    w.Write(@"smfXml.Append(MfString.Format({0} ", q);
                    w.Write(pi.Name);
                    w.Write(@"={0}{1}{0}");
                    w.Write("{0}, {0}{0}{0}{0}, mfi", q);
                    w.Write(f);
                    w.WriteLine(@".ToString()))");
                    w.WriteLine();
                }
                else
                {
                   
                    w.Write(@"smfXml.Append(MfString.Format({0} ", q);
                    w.Write(pi.Name);
                    w.Write(@"={0}{1}{0}");
                    w.Write("{0}, {0}{0}{0}{0}, ", q);
                    w.Write(@"XmlHelper.Encode(this.");
                    w.Write(fkey);
                    w.WriteLine(@")))");
                    w.WriteLine();
                }
               
            }

#if DEBUG
            w.WriteLineNoTabs("; End : WriteToXmlAttributes()");
#endif
        }

        private void WriteToXmlProperties(Type t, ref object csClass, ref Dictionary<string, FieldInfo> fDic)
        {
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteToXmlProperties(Type t, ref object csClass, ref Dictionary<string, FieldInfo> fDic)");
#endif
            PropertyInfo[] props = t.GetProperties(BindingFlags.Public |
                                             BindingFlags.NonPublic |
                                             BindingFlags.Instance); // Obtain all fields
            Dictionary<string, PropertyInfo> fp = new Dictionary<string, PropertyInfo>();
            int i = 0;
            foreach (var prop in props)
            {
                var pt = prop.PropertyType;
                if (pt.IsArray)
                {
                    //w.WriteLine(@"; Value for array {0} will be here.", pi.Name);
                    //TODO: Implement readiing of arrays
                    string ppType = pt.ToString();
                    if (ppType == "System.Object[]")
                    {
                        WriteToXmlPropertyObjectArray(t, prop, ref csClass, ref fDic);
                    }
                    else
                    {
                        WriteToXmlPropertyTypeArray(t, prop, ref csClass, ref fDic);
                    }
                    continue;
                }
                var att = prop.GetCustomAttributes(true);
                if (att == null)
                {
                    continue;
                }
                bool bValidAttrib = true;
                foreach (var attr in att)
                {
                    Type attrType = attr.GetType();
                    if ((attrType == typeof(System.Xml.Serialization.XmlAttributeAttribute)) || (attrType == typeof(System.Xml.Serialization.XmlIgnoreAttribute)))
                    {
                        bValidAttrib &= false;
                    }
                }
                if (bValidAttrib == true)
                {
                    string FieldKey = this.GetFieldKey(prop, ref fDic);
                    if (string.IsNullOrEmpty(FieldKey))
                    {
                        continue;
                    }
                    i++;
                    if (i == 1)
                    {
                        w.WriteLine(@"; Read Property Values");
                    }
                    WriteToXmlProperty(t, prop, FieldKey, ref csClass);
                }
            }
#if DEBUG
            w.WriteLineNoTabs("; End : WriteToXmlProperties(Type t, ref object csClass, ref Dictionary<string, FieldInfo> fDic)");
#endif
        }

        private void WriteToXmlProperty(Type t, PropertyInfo pi, string FieldKey, ref object csClass)
        {
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteToXmlProperties(Type t, PropertyInfo pi, string FieldKey, ref object csClass)");
#endif
            string f = Helper.UpperCaseFirst(FieldKey);
            Type pt = pi.PropertyType;
            TypeCode tc = Type.GetTypeCode(pi.PropertyType);

            if (pt.IsEnum)
            {
                w.Write(@"smfXml.Append({0}<", q);
                w.Write(pi.Name);
                w.WriteLine(@">{0})", q);
                w.Write(@"smfXml.Append(XmlHelper.Encode(this.");
                w.Write(FieldKey);
                w.Write(@".ToString())");
                w.WriteLine(@")");

                w.Write(@"smfXml.AppendLine({0}</", q);
                w.Write(pi.Name);
                w.WriteLine(@">{0})", q);
                w.WriteLine();
            }
            else if(tc == TypeCode.Object)
            {
                w.Write(@"smfXml.Append({0}<", q);
                w.Write(pi.Name);
                w.WriteLine(@">{0})", q);
                w.Write(@"smfXml.Append(this.");
                w.Write(FieldKey);
                w.WriteLine(@".ToXml())");

                w.Write(@"smfXml.AppendLine({0}</", q);
                w.Write(pi.Name);
                w.WriteLine(@">{0})", q);
                w.WriteLine();
            }
            else
            {
                w.Write(@"smfXml.Append({0}<", q);
                w.Write(pi.Name);
                w.WriteLine(@">{0})", q);
                w.Write(@"smfXml.Append(XmlHelper.Encode(this.");
                w.Write(FieldKey);
                w.WriteLine(@"))");

                w.Write(@"smfXml.AppendLine({0}</", q);
                w.Write(pi.Name);
                w.WriteLine(@">{0})", q);
                w.WriteLine();
            }


            //if (pt == typeof(bool))
            //{
            //}
            //else if (pt == typeof(int))
            //{
            //}
            //else
            //{
            //}
#if DEBUG
            w.WriteLineNoTabs("; End : WriteToXmlProperties(Type t, PropertyInfo pi, string FieldKey, ref object csClass)");
#endif
        }

        private void WriteToXmlPropertyObjectArray(Type t, PropertyInfo pi, ref object csClass, ref Dictionary<string, FieldInfo> fDic)
        {
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteToXmlPropertyObjectArray()");
#endif
            string FieldKey = this.GetFieldKey(pi, ref fDic);

            WriteToXmlPropertyArrayItem(t.Name, FieldKey, pi.Name, pi.Name);

            //object[] ArrayAttributes = pi.GetCustomAttributes(typeof(System.Xml.Serialization.XmlArrayItemAttribute), true);
            //if (ArrayAttributes == null)
            //{
            //    return;
            //}
            //foreach (object item in ArrayAttributes)
            //{

            //    System.Xml.Serialization.XmlArrayItemAttribute attr = (System.Xml.Serialization.XmlArrayItemAttribute)item;
            //    string FieldKey = this.GetFieldKey(pi, ref fDic);
            //    if (string.IsNullOrEmpty(FieldKey))
            //    {
            //        return;
            //    }
            //    WriteToXmlPropertyArrayItem(t.Name, FieldKey, pi.Name, attr.ElementName);
            //}

            //object[] ElementAttributes = pi.GetCustomAttributes(typeof(System.Xml.Serialization.XmlElementAttribute), true);
            //if (ElementAttributes == null)
            //{
            //    return;
            //}
            //foreach (object item in ElementAttributes)
            //{

            //    System.Xml.Serialization.XmlElementAttribute attr = (System.Xml.Serialization.XmlElementAttribute)item;
            //    string FieldKey = this.GetFieldKey(pi, ref fDic);
            //    if (string.IsNullOrEmpty(FieldKey))
            //    {
            //        return;
            //    }
            //    WriteToXmlPropertyListElement(t.Name, FieldKey, pi.Name, attr.ElementName);
            //}
#if DEBUG
            w.WriteLineNoTabs("; End : WriteToXmlPropertyObjectArray()");
#endif
        }

        private void WriteToXmlPropertyArrayItem(string ClassName, string FieldName, string PropertyName, string PropertyTypeName)
        {
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteToXmlPropertyArrayItem()");
#endif
            // string f = Helper.UpperCaseFirst(FieldName);

            WriteToXmlPropertyListElement(ClassName, FieldName, PropertyName, PropertyTypeName);
#if DEBUG
            w.WriteLineNoTabs("; End : WriteToXmlPropertyArrayItem()");
#endif
        }


        private void WriteToXmlPropertyListElement(string ClassName, string FieldName, string PropertyName, string PropertyTypeName)
        {
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteToXmlPropertyListElement()");
#endif
            //string f = Helper.UpperCaseFirst(FieldName);
           
            w.Write(@"for i, element in this.");
            w.WriteLine(FieldName);
            w.WriteLine(bo);
            w.Indent++;
            w.Write(@"smfXml.AppendLine(");
            w.WriteLine(@"element.ToXml())");
            w.Indent--;
            w.WriteLine(bc);
                       
            w.WriteLine();
#if DEBUG
            w.WriteLineNoTabs("; End : WriteToXmlPropertyListElement()");
#endif
        }

        private void WriteToXmlPropertyTypeArray(Type t, PropertyInfo pi, ref object csClass, ref Dictionary<string, FieldInfo> fDic)
        {
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteToXmlPropertyTypeArray()");
#endif

           

            object[] ArrayItemAttribs = pi.GetCustomAttributes(typeof(System.Xml.Serialization.XmlArrayItemAttribute), true);
            if (ArrayItemAttribs == null)
            {
                return;
            }
            bool IsEnumArray = false;
            if (pi.PropertyType.IsArray)
            {
                Type underLye = pi.PropertyType.GetElementType();

                if (underLye != null && underLye.IsEnum == true)
                {
                    IsEnumArray = true;
                }
            }
            if (IsEnumArray == true)
            {
                WriteToXmlPropertyTypeEnumArray(t, pi, ref csClass, ref fDic);
                return;
            }
           
            foreach (object item in ArrayItemAttribs)
            {

                System.Xml.Serialization.XmlArrayItemAttribute attr = (System.Xml.Serialization.XmlArrayItemAttribute)item;
                string FieldKey = this.GetFieldKey(pi, ref fDic);
                if (string.IsNullOrEmpty(FieldKey))
                {
                    return;
                }
                string f = Helper.UpperCaseFirst(FieldKey);

                WriteToXmlPropertyArrayItem(t.Name, FieldKey, pi.Name, attr.ElementName);

            }
            object[] ElementItemAttribs = pi.GetCustomAttributes(typeof(System.Xml.Serialization.XmlElementAttribute), true);
            if (ElementItemAttribs == null)
            {
                return;
            }
            foreach (object item in ElementItemAttribs)
            {

                System.Xml.Serialization.XmlElementAttribute attr = (System.Xml.Serialization.XmlElementAttribute)item;
                string FieldKey = this.GetFieldKey(pi, ref fDic);
                if (string.IsNullOrEmpty(FieldKey))
                {
                    return;
                }
                string f = Helper.UpperCaseFirst(FieldKey);

                WriteToXmlPropertyListElement(t.Name, FieldKey, pi.Name, attr.ElementName);

            }

#if DEBUG
            w.WriteLineNoTabs("; End : WriteToXmlPropertyTypeArray()");
#endif
        }
        /// <summary>
        /// Writes ToXml Enum Arrays
        /// </summary>
        /// <param name="t"></param>
        /// <param name="pi"></param>
        /// <param name="csClass"></param>
        /// <param name="fDic"></param>
        private void WriteToXmlPropertyTypeEnumArray(Type t, PropertyInfo pi, ref object csClass, ref Dictionary<string, FieldInfo> fDic)
        {
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteToXmlPropertyTypeEnumArray()");
#endif
            object[] ArrayItemAttribs = pi.GetCustomAttributes(typeof(System.Xml.Serialization.XmlArrayItemAttribute), true);
            if (ArrayItemAttribs == null)
            {
                return;
            }

            string ElementName = string.Empty;
            foreach (object item in ArrayItemAttribs)
            {
                System.Xml.Serialization.XmlArrayItemAttribute attr = (System.Xml.Serialization.XmlArrayItemAttribute)item;
                ElementName = attr.ElementName;
                break;
            }
            if (string.IsNullOrEmpty(ElementName))
            {
                return;
            }

            string FieldKey = this.GetFieldKey(pi, ref fDic);
                 

            w.Write(@"smfXml.Append({0}<", q);
            w.Write(pi.Name);
            w.WriteLine(@">{0})", q);

            w.Write(@"for i, element in this.");
            w.WriteLine(FieldKey);
            w.WriteLine(bo);
            w.Indent++;

            w.Write(@"smfXml.Append({0}<", q);
            w.Write(ElementName);
            w.WriteLine(@">{0})", q);

            w.Write(@"smfXml.Append(");
            w.Write(@"element.ToString()");
            w.WriteLine(@")");

            w.Write(@"smfXml.AppendLine({0}</", q);
            w.Write(ElementName);
            w.WriteLine(@">{0})", q);


            w.Indent--;
            w.WriteLine(bc);

            w.Write(@"smfXml.AppendLine({0}</", q);
            w.Write(pi.Name);
            w.WriteLine(@">{0})", q);

#if DEBUG
            w.WriteLineNoTabs("; End : WriteToXmlPropertyTypeEnumArray()");
#endif
        }
        #endregion

        #region Import XML
        private void WriteParseXml(Type t, ref object csClass, ref Dictionary<string, FieldInfo> fDic)
        {
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteParseXml()");
#endif
            if (t.IsClass == false)
            {
                return;
            }
            

            w.Write(@"ParseXml(sXml) ");
            w.WriteLine(bo);
            w.Indent++;
            w.WriteLine(@"this.VerifyIsInstance(this, A_LineFile, A_LineNumber, A_ThisFunc)");
            w.WriteLine(@"if (MfString.IsNullOrEmpty(sXml))");
            w.WriteLine(bo);
            w.Indent++;
            w.WriteLine(@"ex := new MfArgumentException(MfEnvironment.Instance.GetResourceString({0}ArgumentNull_Generic{0}, {0}sXml{0}))", q);
            w.WriteLine(@"ex.SetProp(A_LineFile, A_LineNumber, A_ThisFunc)");
            w.WriteLine(@"throw ex");
            w.Indent--;
            w.WriteLine(bc);
            w.WriteLine(@"try");
            w.WriteLine(bo);

            w.Indent++;
            w.WriteLine(@"_xmlStr := MfString.GetValue(sXml)");
            w.WriteLine(@"xml := Null");
            w.WriteLine(@"xmlResult := XmlHelper.xpath_load(xml, _xmlStr)");
            w.WriteLine(@"if (xmlResult = 0)");
            w.WriteLine(bo);
            w.Indent++;
            w.WriteLine(@"ex := new MfFormatException(MfEnvironment.Instance.GetResourceString({0}FormatException_UnableToLoad{0}, {0}XML{0}, A_ThisFunc))", q);
            w.WriteLine(@"ex.SetProp(A_LineFile, A_LineNumber, A_ThisFunc)");
            w.WriteLine(@"throw ex");
            w.Indent--;
            w.WriteLine(bc);
            this.WriteParseXmlClears(t, ref fDic);
            w.WriteLine();
            WriteParseXmlProperties(t, ref csClass, ref fDic);
            WriteParseXmlAttributes(t, ref csClass, ref fDic);
            w.Indent--;
            w.WriteLine(bc);
            w.WriteLine(@"catch e");
            w.WriteLine(bo);
            w.Indent++;
            w.WriteLine(@"ex := new MfException(MfString.Format(MfEnvironment.Instance.GetResourceString({0}Exception_Error{0}), A_ThisFunc), e)", q);
            w.WriteLine(@"ex.SetProp(A_LineFile, A_LineNumber, A_ThisFunc)");
            w.WriteLine(@"throw ex");
            w.Indent--;
            w.WriteLine(bc);
            w.Indent--;
            w.WriteLine(bc);
            w.WriteLine();
#if DEBUG
            w.WriteLineNoTabs("; End : WriteParseXml()");
#endif
        }
        // writes the code to clear any arrays or list in the class
        private void WriteParseXmlClears(Type t, ref Dictionary<string, FieldInfo> fDic)
        {
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteParseXmlClears()");
#endif
            PropertyInfo[] props = t.GetProperties(BindingFlags.Public |
                                             BindingFlags.NonPublic |
                                             BindingFlags.Instance); // Obtain all fields

            List<string> Fields = new List<string>();

            foreach (var prop in props)
            {
                if (prop.PropertyType.IsArray)
                {
                    string FieldKey = this.GetFieldKey(prop, ref fDic);
                    if (string.IsNullOrEmpty(FieldKey))
                    {
                        continue;
                    }
                    Fields.Add(FieldKey);
                   
                }
            }
            if (Fields.Count > 0)
            {
                w.WriteLine(@"; Clear out list before reading new xml");
            }
            foreach (string f in Fields)
            {
                w.Write(@"this.");
                w.Write(f);
                w.WriteLine(@".Clear()");
            }
            if (Fields.Count > 0)
            {
                w.WriteLine();
            }
#if DEBUG
            w.WriteLineNoTabs("; End : WriteParseXmlClears()");
#endif
        }

        private void WriteParseXmlAttributes(Type t, ref object csClass, ref Dictionary<string, FieldInfo> fDic)
        {
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteParseXmlAttributes()");
#endif
            PropertyInfo[] props = t.GetProperties(BindingFlags.Public |
                                             BindingFlags.NonPublic |
                                             BindingFlags.Instance); // Obtain all fields
            Dictionary<string, PropertyInfo> fp = new Dictionary<string, PropertyInfo>();

            foreach (var prop in props)
            {
                var pt = prop.PropertyType;


                var att = prop.GetCustomAttributes(typeof(System.Xml.Serialization.XmlAttributeAttribute), true);
                if (att.Count() > 0)
                {
                    string FieldKey = this.GetFieldKey(prop, ref fDic);
                    if (string.IsNullOrEmpty(FieldKey))
                    {
                        continue;
                    }
                    fp.Add(FieldKey, prop);
                }
 
            }

            if (fp.Count > 0)
            {
                if (fp.Count == 1)
                {
                    w.WriteLine(@"; Set Attribute Value");
                }
                else
                {
                    w.WriteLine(@"; Set Attribute Values");
                }
                
            }

            var map = this.maps.Where(m => m.TypeName == t.Name).FirstOrDefault();
            string ElementName;
            if (this.m_SpDic.ContainsKey(t.Name))
            {
                var info = this.m_SpDic[t.Name];
                ElementName = info.ElementName;
            }
            else if (map != null)
            {
                ElementName = map.ElementName;
            }
            else
            {
                ElementName = t.Name;
            }
            foreach (var fkey in fp.Keys)
            {
                PropertyInfo pi = fp[fkey];
                string f = Helper.UpperCaseFirst(fkey);
                Type pt = pi.PropertyType;
                
                
                

                if (pt.IsEnum)
                {
                    var value = pi.GetValue(csClass, null);

                    var att = pi.GetCustomAttributes(true);
                    bool EnumHasFlags = false;
                    if (att != null)
                    {
                        foreach (var attr in att)
                        {
                            Type attrType = attr.GetType();
                            if (attrType == typeof(System.FlagsAttribute))
                            {
                                EnumHasFlags = true;
                                break;
                            }
                        }
                    }

                    var pInfo = csClass.GetType().GetProperty(pi.Name);
                    if (pInfo == null)
                    {
                        return;
                    }
                    object val = pInfo.GetValue(csClass, null);

                    int iValue = (int)val;



                    w.Write(@"str");
                    w.Write(f);
                    w.Write(@" := XmlHelper.xpath(xml, ");
                    w.Write(@"{0}/", q);
                    w.Write(ElementName);
                    w.Write(@"/@");
                    w.Write(pi.Name);
                    w.WriteLine(@"/text(){0})", q);

                    if (this.XmlIgnoreWhiteSpace)
                    {
                        w.Write(@"str");
                        w.Write(f);
                        w.Write(@" := ");
                        w.Write(@"Trim(");
                        w.Write(@"str");
                        w.Write(f);
                        w.Write(@", ");
                        w.Write(AutoHotkeyBuilder.tc);
                        w.WriteLine(@")");
                    }
                   

                  if (EnumHasFlags == true)
                    {
                        w.Write(@"this.");
                        w.Write(fkey);
                        w.Write(@" := new ");
                        w.Write(pInfo.PropertyType.ToString());
                        w.Write(@"(");
                        w.Write(iValue);
                        w.WriteLine(@")");

                        w.Write(@"MfEnum.TryParse(mfs");
                        w.Write(f);
                        w.Write(@", this.");
                        w.Write(fkey);
                        w.WriteLine(@")");
                    }
                    else
                    {
                        w.WriteLine();
                        w.WriteLine(@"try");
                        w.WriteLine(bo);
                        w.Indent++;
                        w.Write(@" this.");
                        w.Write(fkey);
                        w.Write(@" := ");


                        // The following block was added to write code for the now
                        // static method MfEnum.ParseItem()
                        // previously the method was not static
                        w.Write(@"MfEnum.ParseItem(");
                        w.Write(pInfo.PropertyType.ToString());
                        w.Write(@".GetType(), str");
                        w.Write(f);
                        w.WriteLine(@")");

                        //w.Write(pInfo.PropertyType.ToString());
                        //w.Write(@".Instance.ParseItem(mfs");
                        //w.Write(f);
                        //w.WriteLine(@")");

                        w.Indent--;
                        w.WriteLine(bc);

                        w.WriteLine(@"catch e");
                        w.WriteLine(bo);
                        w.Indent++;
                        w.Write(@" this.");
                        w.Write(fkey);
                        w.Write(@" := ");
                        w.Write(pInfo.PropertyType.ToString());
                        w.Write(@".Instance.");
                        w.WriteLine(value.ToString());
                        w.Indent--;
                        w.WriteLine(bc);

                    }
                }
                else if (pt == typeof(bool))
                {
                    var pInfo = csClass.GetType().GetProperty(pi.Name);
                    if (pInfo == null)
                    {
                        continue;
                    }
                  

                    bool bValue = (bool)pInfo.GetValue(csClass, null);

                    w.Write(@"str");
                    w.Write(f);
                    w.Write(@" := XmlHelper.xpath(xml, ");
                    w.Write(@"{0}/", q);
                    w.Write(ElementName);
                    w.Write(@"/@");
                    w.Write(pi.Name);
                    w.WriteLine(@"/text(){0})", q);

                    if (this.XmlIgnoreWhiteSpace)
                    {
                        w.Write(@"str");
                        w.Write(f);
                        w.Write(@" := ");
                        w.Write(@"Trim(");
                        w.Write(@"str");
                        w.Write(f);
                        w.Write(@", ");
                        w.Write(AutoHotkeyBuilder.tc);
                        w.WriteLine(@")");
                    }

                    w.Write(@"if (!MfNull.IsNull(str");
                    w.Write(f);
                    w.WriteLine(@"))");
                    w.WriteLine(bo);
                    w.Indent++;
                    w.Write(@"b");
                    w.Write(f);
                    w.Write(@" := new MfBool(MfBool.");
                    w.Write(bValue.ToString());
                    w.WriteLine(@")");

                    w.Write(@"if(MfBool.TryParse(b");
                    w.Write(f);
                    w.Write(@", str");
                    w.Write(f);
                    w.WriteLine(@"))");

                    w.WriteLine(bo);
                    w.Indent++;
                    w.Write(@"this.");
                    w.Write(fkey);
                    w.Write(@" := b");
                    w.Write(f);
                    w.WriteLine(@".Value");

                    w.Indent--;
                    w.WriteLine(bc);

                    w.Indent--;
                    w.WriteLine(bc);

                    w.WriteLine(@"else");
                    w.WriteLine(bo);
                    w.Indent++;
                    w.Write(@"this.");
                    w.Write(fkey);
                    w.Write(@" := ");
                    w.WriteLine(bValue.ToString().ToLower());
                    w.Indent--;
                    w.WriteLine(bc);
                    w.WriteLine();

                }
                else if (pt == typeof(int))
                {
                    var pInfo = csClass.GetType().GetProperty(pi.Name);
                    if (pInfo == null)
                    {
                        Console.WriteLine("Null Property value:{0} for type:{1}", pi.Name, t.Name);
                        continue;
                    }

                    int iValue = (int)pInfo.GetValue(csClass, null);

                    w.Write(@"str");
                    w.Write(f);
                    w.Write(@" := XmlHelper.xpath(xml, ");
                    w.Write(@"{0}/", q);
                    w.Write(ElementName);
                    w.Write(@"/@");
                    w.Write(pi.Name);
                    w.WriteLine(@"/text(){0})", q);

                    if (this.XmlIgnoreWhiteSpace)
                    {
                        w.Write(@"str");
                        w.Write(f);
                        w.Write(@" := ");
                        w.Write(@"Trim(");
                        w.Write(@"str");
                        w.Write(f);
                        w.Write(@", ");
                        w.Write(AutoHotkeyBuilder.tc);
                        w.WriteLine(@")");
                    }

                    w.Write(@"if (!MfNull.IsNull(str");
                    w.Write(f);
                    w.WriteLine(@"))");
                    w.WriteLine(bo);
                    w.Indent++;
                    w.Write(@"i");
                    w.Write(f);
                    w.Write(@" := new MfInteger(");
                    w.Write(iValue.ToString());
                    w.WriteLine(@")");

                    w.Write(@"if(MfInteger.TryParse(i");
                    w.Write(f);
                    w.Write(@", str");
                    w.Write(f);
                    w.WriteLine(@"))");

                    w.WriteLine(bo);
                    w.Indent++;
                    w.Write(@"this.");
                    w.Write(fkey);
                    w.Write(@" := i");
                    w.Write(f);
                    w.WriteLine(@".Value");

                    w.Indent--;
                    w.WriteLine(bc);

                    w.Indent--;
                    w.WriteLine(bc);

                    w.WriteLine(@"else");
                    w.WriteLine(bo);
                    w.Indent++;
                    w.Write(@"this.");
                    w.Write(fkey);
                    w.Write(@" := ");
                    w.WriteLine(iValue.ToString());
                    w.Indent--;
                    w.WriteLine(bc);
                    w.WriteLine();
                }
                else
                {
                    w.Write(@"str");
                    w.Write(f);
                    w.Write(@" := XmlHelper.xDecode(xml, ");
                    w.Write(@"{0}/", q);
                    w.Write(ElementName);
                    w.Write(@"/@");
                    w.Write(pi.Name);
                    w.WriteLine(@"/text(){0})", q);

                    if (this.XmlIgnoreWhiteSpace)
                    {
                        w.Write(@"str");
                        w.Write(f);
                        w.Write(@" := ");
                        w.Write(@"Trim(");
                        w.Write(@"str");
                        w.Write(f);
                        w.Write(@", ");
                        w.Write(tc);
                        w.WriteLine(@")");
                    }

                    w.Write(@"this.");
                    w.Write(fkey);
                    w.Write(@" := ");
                    w.Write(@"str");
                    w.WriteLine(f);
                   
                }

            }

#if DEBUG
            w.WriteLineNoTabs("; End : WriteParseXmlAttributes()");
#endif
        }

        // t is class type
        // csClass is class instance
        // fDic is all the fields in the class
        private void WriteParseXmlProperties(Type t, ref object csClass, ref Dictionary<string, FieldInfo> fDic)
        {
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteParseXmlProperties()");
#endif
            PropertyInfo[] props = t.GetProperties(BindingFlags.Public |
                                             BindingFlags.NonPublic |
                                             BindingFlags.Instance); // Obtain all fields
            Dictionary<string, PropertyInfo> fp = new Dictionary<string, PropertyInfo>();
            int i = 0;
            foreach (var prop in props)
            {
                var pt = prop.PropertyType;
                if (pt.IsArray)
                {
                    //w.WriteLine(@"; Value for array {0} will be here.", pi.Name);
                    //TODO: Implement reading of arrays
                    string ppType = pt.ToString();
                    if (ppType == "System.Object[]")
                    {
                        WriteParseXmlPropertyObjectArray(t, prop, ref csClass, ref fDic);
                    }
                    else
                    {
                        WriteParseXmlPropertyTypeArray(t, prop, ref csClass, ref fDic);
                    }
                    continue;
                }
                var att = prop.GetCustomAttributes(true);
                if (att == null)
                {
                    continue;
                }
                bool bValidAttrib = true;
     
                foreach (var attr in att)
                {
                    Type attrType = attr.GetType();
                    if ((attrType == typeof(System.Xml.Serialization.XmlAttributeAttribute)) || (attrType == typeof(System.Xml.Serialization.XmlIgnoreAttribute)))
                    {
                        bValidAttrib &= false;
                    }
                }
                // if not an element attribute and Not ignore attribute
                if (bValidAttrib == true)
                {
                    string FieldKey = this.GetFieldKey(prop, ref fDic);
                    if (string.IsNullOrEmpty(FieldKey))
                    {
                        continue;
                    }
                    i++;
                    if (i == 1)
                    {
                        w.WriteLine(@"; Set Property Values");
                    }
                    WriteParseXmlProperty(t, prop, FieldKey, ref csClass);
                }
                else
                {
                    // check an see if this is a specified property and set it value if so
                    if (prop.PropertyType == typeof(bool) && prop.Name.EndsWith(@"Specified"))
                    {
                        string FieldKey = prop.Name.Substring(0, prop.Name.Length - 9) + "FieldSpecified";
                        
                        if (!fDic.ContainsKey(FieldKey))
                        {
                            FieldKey = Helper.LowerCaseFirst(FieldKey);
                        }
                        if (!fDic.ContainsKey(FieldKey))
                        {
                            continue;
                        }
                        if (string.IsNullOrEmpty(FieldKey))
                        {
                            continue;
                        }
                        WriteParseXmlPropertySpecified(t, prop, FieldKey);

                    }
                }
            }
#if DEBUG
            w.WriteLineNoTabs("; End : WriteParseXmlProperties()");
#endif
        }

        private void WriteParseXmlPropertySpecified(Type t, PropertyInfo pi, string FieldKey)
        {
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteParseXmlPropertySpecified()");
#endif


            string f = Helper.UpperCaseFirst(FieldKey);
            Type pt = pi.PropertyType;
            string pp = pi.Name.Substring(0, pi.Name.Length - 9);

            string ElementName;
            var map = this.maps.Where(m => m.TypeName == t.Name).FirstOrDefault();
            
            if (this.m_SpDic.ContainsKey(t.Name))
            {
                var info = this.m_SpDic[t.Name];
                ElementName = info.ElementName;
            }
            else if (map != null)
            {
                ElementName = map.ElementName;
            }
            else
            {
                ElementName = t.Name;
            }


            var forProp = t.GetProperty(pp, BindingFlags.Public |
                                             BindingFlags.NonPublic |
                                             BindingFlags.Instance);
            if (forProp == null)
            {
                return;
            }

            var att = forProp.GetCustomAttributes(true);
            bool bIsAttribute = false;
            if (att != null)
            {
                foreach (var attr in att)
                {
                    Type attrType = attr.GetType();
                    if (attrType == typeof(System.Xml.Serialization.XmlAttributeAttribute))
                    {
                        bIsAttribute = true;
                        break;
                       
                    }
                }
            }
           

            w.Write(@"mfs");
            w.Write(f);
            w.Write(@" := new MfString(XmlHelper.xpath(xml, ");
            w.Write(@"{0}/", q);
            w.Write(ElementName);
            w.Write(@"/");
            if (bIsAttribute)
            {
                w.Write(@"@");

            }
            w.Write(pp);
            w.WriteLine(@"/text(){0}))", q);

            if (this.XmlIgnoreWhiteSpace)
            {
                w.Write(@"mfs");
                w.Write(f);
                w.Write(@".Trim(");
                w.Write(AutoHotkeyBuilder.tc);
                w.WriteLine(@")");
            }

            w.Write(@"this.");
            w.Write(FieldKey);
            w.Write(@" := (mfs");
            w.Write(f);
            w.WriteLine(@".Length > 0)");
            w.WriteLine();
#if DEBUG
            w.WriteLineNoTabs("; End : WriteParseXmlPropertySpecified()");
#endif

        }

        // t is class type
        private void WriteParseXmlProperty(Type t, PropertyInfo pi, string FieldKey, ref object csClass)
        {

#if DEBUG
            w.WriteLineNoTabs("; Start : WriteParseXmlProperty()");
#endif

            string f = Helper.UpperCaseFirst(FieldKey);
            object[] ArrayAttributes = csClass.GetType().GetCustomAttributes(typeof(System.Xml.Serialization.XmlRootAttribute), true);

            Type pt = pi.PropertyType;
            TypeCode tc = Type.GetTypeCode(pt);
            PropertyInfo pInfo;

            var cAttribs = pi.GetCustomAttributes(true);
            bool bIsTextAttribute = false;
            if (cAttribs != null)
            {
                foreach (var attr in cAttribs)
                {
                    Type attrType = attr.GetType();
                   
                    if (attrType == typeof(System.Xml.Serialization.XmlTextAttribute))
                    {
                        bIsTextAttribute = true;
                        break;

                    }
                }
            }

            // when an element has XmlTextAttribute applied then that element will not have subitems
            // it seem to be a rare case to have XmlTextAttribute applied.
            // I ran across this when working with AutoHotkey Snippit and the Hotstring element
            // XPath: /hotstrings/hotstring/replacements/inputFixedList/listValues/listItem
            // The element has a custom global type and the global type base was xs:string.


            string ElementName;
            var map = this.maps.Where(m => m.TypeName == t.Name).FirstOrDefault();
            if (this.m_SpDic.ContainsKey(t.Name))
            {
                var info = this.m_SpDic[t.Name];
                ElementName = info.ElementName;
            }
            else if (map != null)
            {
                ElementName = map.ElementName;
            }
            else
            {
                ElementName = t.Name;
            }

            if (pt.IsEnum)
            {
                var value = pi.GetValue(csClass, null);

                var att = pi.GetCustomAttributes(true);
                bool EnumHasFlags = false;
                if (att != null)
                {
                    foreach (var attr in att)
                    {
                        Type attrType = attr.GetType();
                        if (attrType == typeof(System.FlagsAttribute))
                        {
                            EnumHasFlags = true;
                            break;
                        }
                    }
                }

                pInfo = csClass.GetType().GetProperty(pi.Name);
                if (pInfo == null)
                {
                    return;
                }
                object val = pInfo.GetValue(csClass, null);

                int iValue = (int)val;

                w.Write(@"mfs");
                w.Write(f);
                w.Write(@" := new MfString(XmlHelper.xpath(xml, ");
                w.Write(@"{0}/", q);
                w.Write(ElementName);
                w.Write(@"/");
                w.Write(pi.Name);
                w.WriteLine(@"/text(){0}))", q);

                if (this.XmlIgnoreWhiteSpace)
                {
                    w.Write(@"mfs");
                    w.Write(f);
                    w.Write(@".Trim(");
                    w.Write(AutoHotkeyBuilder.tc);
                    w.WriteLine(@")");
                }
               


                if (EnumHasFlags == true)
                {
                    w.Write(@"this.");
                    w.Write(FieldKey);
                    w.Write(@" := new ");
                    w.Write(pInfo.PropertyType.ToString());
                    w.Write(@"(");
                    w.Write(iValue);
                    w.WriteLine(@")");

                    w.Write(@"MfEnum.TryParse(mfs");
                    w.Write(f);
                    w.Write(@", this.");
                    w.Write(FieldKey);
                    w.WriteLine(@")");
                }
                else
                {
                    w.WriteLine();
                    w.WriteLine(@"try");
                    w.WriteLine(bo);
                    w.Indent++;
                    w.Write(@" this.");
                    w.Write(FieldKey);
                    w.Write(@" := ");

                    // The following block was added to write code for the now
                    // static method MfEnum.ParseItem()
                    // previously the method was not static
                    w.Write(@"MfEnum.ParseItem(");
                    w.Write(pInfo.PropertyType.ToString());
                    w.Write(@".GetType(), mfs");
                    w.Write(f);
                    w.WriteLine(@")");

                    //w.Write(pInfo.PropertyType.ToString());
                    //w.Write(@".Instance.ParseItem(mfs");
                    //w.Write(f);
                    //w.WriteLine(@")");
                    w.Indent--;
                    w.WriteLine(bc);

                    w.WriteLine(@"catch e");
                    w.WriteLine(bo);
                    w.Indent++;
                    w.Write(@" this.");
                    w.Write(FieldKey);
                    w.Write(@" := ");
                    w.Write(pInfo.PropertyType.ToString());
                    w.Write(@".Instance.");
                    w.WriteLine(value.ToString());
                    w.Indent--;
                    w.WriteLine(bc);

                }
               

                w.WriteLine();
                return;

            }
            
            switch (tc)
            {

                case TypeCode.Object:
                    w.Write(@"s");
                    w.Write(f);
                    w.Write(@" := XmlHelper.xpath(xml, ");
                    w.Write(@"{0}/", q);
                    w.Write(ElementName);

                    if (bIsTextAttribute == false)
                    {
                        w.Write(@"/");
                        w.Write(pi.Name);
                    }
                    w.WriteLine(@"{0})", q);

                    //w.Write(@"mfs");
                    //w.Write(f);
                    //w.WriteLine(@".Trim()");

                    //w.Write(@"if (mfs");
                    //w.Write(f);
                    //w.WriteLine(@".Length > 0)");
                    //w.WriteLine(bo);
                    //w.Indent++;

                    w.Write(@"this.");
                    w.Write(FieldKey);
                    w.Write(@".ParseXml(");
                    w.Write(@"s");
                    w.Write(f);
                    w.WriteLine(@")");

                    //w.Indent--;
                    //w.WriteLine(bc);


                   
                    break;
                case TypeCode.Boolean:
                    pInfo = csClass.GetType().GetProperty(pi.Name);
                    if (pInfo == null)
                    {
                        //Console.WriteLine("Null Property value:{0} for type:{1}", pi.Name, t.Name);
                        return;
                    }


                    bool bValue = (bool)pInfo.GetValue(csClass, null);

                    w.Write(@"str");
                    w.Write(f);
                    w.Write(@" := XmlHelper.xpath(xml, ");
                    w.Write(@"{0}/", q);
                    w.Write(ElementName);
                    w.Write(@"/");
                    
                    if (bIsTextAttribute == false)
                    {
                        w.Write(pi.Name);
                        w.Write(@"/");
                    }
                                        
                    w.WriteLine(@"text(){0})", q);

                    w.Write(@"if (!MfNull.IsNull(str");
                    w.Write(f);
                    w.WriteLine(@"))");
                    w.WriteLine(bo);
                    w.Indent++;
                    w.Write(@"b");
                    w.Write(f);
                    w.Write(@" := new MfBool(MfBool.");
                    w.Write(bValue.ToString());
                    w.WriteLine(@")");

                    w.Write(@"if(MfBool.TryParse(b");
                    w.Write(f);
                    w.Write(@", str");
                    w.Write(f);
                    w.WriteLine(@"))");

                    w.WriteLine(bo);
                    w.Indent++;
                    w.Write(@"this.");
                    w.Write(FieldKey);
                    w.Write(@" := b");
                    w.Write(f);
                    w.WriteLine(@".Value");

                    w.Indent--;
                    w.WriteLine(bc);

                    w.Indent--;
                    w.WriteLine(bc);

                    w.WriteLine(@"else");
                    w.WriteLine(bo);
                    w.Indent++;
                    w.Write(@"this.");
                    w.Write(FieldKey);
                    w.Write(@" := ");
                    w.WriteLine(bValue.ToString().ToLower());
                    w.Indent--;
                    w.WriteLine(bc);
                    w.WriteLine();
                    break;
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    pInfo = csClass.GetType().GetProperty(pi.Name);
                    if (pInfo == null)
                    {
                        //Console.WriteLine("Null Property value:{0} for type:{1}", pi.Name, t.Name);
                        return;
                    }

                    decimal fValue = (decimal)pInfo.GetValue(csClass, null);

                    w.Write(@"str");
                    w.Write(f);
                    w.Write(@" := XmlHelper.xpath(xml, ");
                    w.Write(@"{0}/", q);
                    w.Write(ElementName);
                    w.Write(@"/");
                   
                    if (bIsTextAttribute == false)
                    {
                        w.Write(pi.Name);
                        w.Write(@"/");
                    }
                   
                    w.WriteLine(@"text(){0})", q);

                    w.Write(@"if (!MfNull.IsNull(str");
                    w.Write(f);
                    w.WriteLine(@"))");
                    w.WriteLine(bo);
                    w.Indent++;
                    w.Write(@"f");
                    w.Write(f);
                    w.Write(@" := new MfFloat(");
                    w.Write(fValue.ToString());
                    w.WriteLine(@")");

                    w.Write(@"if(MfFloat.TryParse(f");
                    w.Write(f);
                    w.Write(@", str");
                    w.Write(f);
                    w.WriteLine(@"))");

                    w.WriteLine(bo);
                    w.Indent++;
                    w.Write(@"this.");
                    w.Write(FieldKey);
                    w.Write(@" := f");
                    w.Write(f);
                    w.WriteLine(@".Value");

                    w.Indent--;
                    w.WriteLine(bc);

                    w.Indent--;
                    w.WriteLine(bc);

                    w.WriteLine(@"else");
                    w.WriteLine(bo);
                    w.Indent++;
                    w.Write(@"this.");
                    w.Write(FieldKey);
                    w.Write(@" := ");
                    w.WriteLine(fValue.ToString());
                    w.Indent--;
                    w.WriteLine(bc);
                    w.WriteLine();
                    break;
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                //case TypeCode.UInt16:
                case TypeCode.Int32:
                //case TypeCode.UInt32:
                case TypeCode.Int64:
                //case TypeCode.UInt64:
                
                    pInfo = csClass.GetType().GetProperty(pi.Name);
                    if (pInfo == null)
                    {
                        //Console.WriteLine("Null Property value:{0} for type:{1}", pi.Name, t.Name);
                        return;
                    }

                    int iValue = (int)pInfo.GetValue(csClass, null);

                    w.Write(@"str");
                    w.Write(f);
                    w.Write(@" := XmlHelper.xpath(xml, ");
                    w.Write(@"{0}/", q);
                    w.Write(ElementName);
                    w.Write(@"/");
                    
                    if (bIsTextAttribute == false)
                    {
                        w.Write(pi.Name);
                        w.Write(@"/");
                    }

                    w.WriteLine(@"text(){0})", q);

                    w.Write(@"if (!MfNull.IsNull(str");
                    w.Write(f);
                    w.WriteLine(@"))");
                    w.WriteLine(bo);
                    w.Indent++;
                    w.Write(@"i");
                    w.Write(f);
                    w.Write(@" := new MfInteger(");
                    w.Write(iValue.ToString());
                    w.WriteLine(@")");

                    w.Write(@"if(MfInteger.TryParse(i");
                    w.Write(f);
                    w.Write(@", str");
                    w.Write(f);
                    w.WriteLine(@"))");

                    w.WriteLine(bo);
                    w.Indent++;
                    w.Write(@"this.");
                    w.Write(FieldKey);
                    w.Write(@" := i");
                    w.Write(f);
                    w.WriteLine(@".Value");

                    w.Indent--;
                    w.WriteLine(bc);

                    w.Indent--;
                    w.WriteLine(bc);

                    w.WriteLine(@"else");
                    w.WriteLine(bo);
                    w.Indent++;
                    w.Write(@"this.");
                    w.Write(FieldKey);
                    w.Write(@" := ");
                    w.WriteLine(iValue.ToString());
                    w.Indent--;
                    w.WriteLine(bc);
                    w.WriteLine();
                    break;
                case TypeCode.DateTime:
                case TypeCode.Char:
                case TypeCode.String:
                case TypeCode.DBNull:
                case TypeCode.Empty:
                default:
                    w.Write(@"mfs");
                    w.Write(f);
                    w.Write(@" := new MfString(XmlHelper.xDecode(xml, ");
                    w.Write(@"{0}/", q);
                    w.Write(ElementName);
                    w.Write(@"/");

                    if (bIsTextAttribute == false)
                    {
                        w.Write(pi.Name);
                        w.Write(@"/");
                    }
                    w.WriteLine(@"text(){0}))", q);

                    if (this.XmlIgnoreWhiteSpace)
                    {
                        w.Write(@"mfs");
                        w.Write(f);
                        w.Write(@".Trim(");
                        w.Write(AutoHotkeyBuilder.tc);
                        w.WriteLine(@")");
                    }
                 

                    w.Write(@"this.");
                    w.Write(FieldKey);
                    w.Write(@" := ");
                    w.Write(@"mfs");
                    w.Write(f);
                    w.WriteLine(@".Value");
                    w.WriteLine();
                    break;
            }
#if DEBUG
            w.WriteLineNoTabs("; End : WriteParseXmlProperty()");
#endif

        }
        // this method is called with the property being written into ParseXml Ahk method is
        // know to be an object type array
        // This method gets the allowed types for the object array base upon its attributes
        private void WriteParseXmlPropertyObjectArray(Type t, PropertyInfo pi, ref object csClass, ref Dictionary<string, FieldInfo> fDic)
        {
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteParseXmlPropertyObjectArray()");
#endif
            object[] ArrayAttributes = pi.GetCustomAttributes(typeof(System.Xml.Serialization.XmlArrayItemAttribute), true);
            if (ArrayAttributes == null)
            {
                return;
            }
            foreach (object item in ArrayAttributes)
            {

                System.Xml.Serialization.XmlArrayItemAttribute attr = (System.Xml.Serialization.XmlArrayItemAttribute)item;
                string FieldKey = this.GetFieldKey(pi, ref fDic);
                if (string.IsNullOrEmpty(FieldKey))
                {
                    return;
                }
                WriteParseXmlPropertyArrayItem(t.Name, FieldKey, pi, attr.ElementName);
            }

            object[] ElementAttributes = pi.GetCustomAttributes(typeof(System.Xml.Serialization.XmlElementAttribute), true);
            if (ElementAttributes == null)
            {
                return;
            }
            foreach (object item in ElementAttributes)
            {

                System.Xml.Serialization.XmlElementAttribute attr = (System.Xml.Serialization.XmlElementAttribute)item;
                string FieldKey = this.GetFieldKey(pi, ref fDic);
                if (string.IsNullOrEmpty(FieldKey))
                {
                    return;
                }
                WriteParseXmlPropertyListElement(t, FieldKey, pi, attr);
            }

#if DEBUG
            w.WriteLineNoTabs("; End : WriteParseXmlPropertyObjectArray()");
#endif
        }

        // t is class type
        // pi is class property type
        // csClass is class instance
        // fDic is all the fields in the class
        private void WriteParseXmlPropertyTypeArray(Type t, PropertyInfo pi, ref object csClass, ref Dictionary<string, FieldInfo> fDic)
        {
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteParseXmlPropertyTypeArray()");
#endif
            object[] ArrayItemAttribs = pi.GetCustomAttributes(typeof(System.Xml.Serialization.XmlArrayItemAttribute), true);
            if (ArrayItemAttribs == null)
            {
                return;
            }
            foreach (object item in ArrayItemAttribs)
            {

                System.Xml.Serialization.XmlArrayItemAttribute attr = (System.Xml.Serialization.XmlArrayItemAttribute)item;
                string FieldKey = this.GetFieldKey(pi, ref fDic);
                if (string.IsNullOrEmpty(FieldKey))
                {
                    return;
                }
                string f = Helper.UpperCaseFirst(FieldKey);

                WriteParseXmlPropertyArrayItem(t.Name, FieldKey, pi, attr.ElementName);

            }
            object[] ElementItemAttribs = pi.GetCustomAttributes(typeof(System.Xml.Serialization.XmlElementAttribute), true);
            if (ElementItemAttribs == null)
            {
                return;
            }
            foreach (object item in ElementItemAttribs)
            {

                System.Xml.Serialization.XmlElementAttribute attr = (System.Xml.Serialization.XmlElementAttribute)item;
                string FieldKey = this.GetFieldKey(pi, ref fDic);
                if (string.IsNullOrEmpty(FieldKey))
                {
                    return;
                }
                string f = Helper.UpperCaseFirst(FieldKey);

                WriteParseXmlPropertyListElement(t, FieldKey, pi, attr);

            }

#if DEBUG
            w.WriteLineNoTabs("; End : WriteParseXmlPropertyTypeArray()");
#endif
        }

        // pi is class property type
        private void WriteParseXmlPropertyArrayItem(string ClassName, string FieldName, PropertyInfo pi, string ElementName)
        {
            // is some cases the array item here will be of type object
            string f = Helper.UpperCaseFirst(FieldName);
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteParseXmlPropertyArrayItem()");
            w.WriteLine(string.Format("; property type{0}", pi.PropertyType.ToString()));
#endif
            string PropertyTypeName = pi.PropertyType.Name.Substring(0, pi.PropertyType.Name.Length - 2);

            w.Write(@"iCount := XmlHelper.xpath(xml, {0}/", q);
            w.Write(ClassName);
            w.Write(@"/");
            w.Write(pi.Name);
            w.Write(@"/");
            w.Write(ElementName);
            w.WriteLine(@"/Count(){0})", q);

            w.WriteLine(@"Loop,%iCount%");
            w.WriteLine(bo);
            w.Indent++;
            w.Write(@"mfs");
            w.Write(f);
            w.Write(@" := new MfString(XmlHelper.xpath(xml, MfString.Format({0}/", q);
            w.Write(ClassName);
            w.Write(@"/");
            w.Write(pi.Name);
            w.Write(@"/");
            w.Write(ElementName);
            w.Write(@"[{0}]");
            w.WriteLine(@"{0}, A_Index)))", q);

            if (this.XmlIgnoreWhiteSpace)
            {
                w.Write(@"mfs");
                w.Write(f);
                w.Write(@".Trim(");
                w.Write(AutoHotkeyBuilder.tc);
                w.WriteLine(@")");
            }
          
            w.Write(@"if (mfs");
            w.Write(f);
            w.WriteLine(@".Length > 0)");
            w.WriteLine(bo);
            w.Indent++;

           

            // if this is an array of Enums we do not want to
            // pass any xml to the constructor
            Type underLye = pi.PropertyType.GetElementType();
            if (underLye != null && underLye.IsEnum == true)
            {
                w.WriteLine(@"xmlEnum := Null");
                w.Write(@"EnumXmlResult := XmlHelper.xpath_load(xmlEnum, ");
                w.Write(@"mfs");
                w.Write(f);
                w.WriteLine(".Value)");

                w.WriteLine(@"if (EnumXmlResult = 0)");
                w.WriteLine(bo);
                w.Indent++;
                w.WriteLine("ex := new MfFormatException(MfEnvironment.Instance.GetResourceString(\"FormatException_UnableToLoad\", \"XML\", A_ThisFunc))");
                w.WriteLine(@"ex.SetProp(A_LineFile, A_LineNumber, A_ThisFunc)");
                w.WriteLine(@"throw ex");
                w.Indent--;
                w.WriteLine(bc);

                w.Write(@"EnumTextValue := XmlHelper.xpath(xmlEnum, ");
                w.Write("\"/");
                w.Write(ElementName);
                w.WriteLine("/text()\")");

                w.WriteLine("try");
                w.WriteLine(bo);
                w.Indent++;

                w.Write(@"obj");
                w.Write(f);
                w.Write(@" := ");

                // The following block was added to write code for the now
                // static method MfEnum.ParseItem()
                // previously the method was not static
                w.Write(@"MfEnum.ParseItem(");
                w.Write(PropertyTypeName);
                w.Write(@".GetType(), EnumTextValue, true");
                w.WriteLine(@")");

                //w.Write(PropertyTypeName);
                //w.WriteLine(".Instance.ParseItem(EnumTextValue, true)");

                w.Write(@"this.");
                w.Write(FieldName);
                w.Write(@".Add(");
                w.Write(@"obj");
                w.Write(f);
                w.WriteLine(@")");

                w.Indent--;
                w.WriteLine(bc);
                w.WriteLine("catch e");
                w.WriteLine(bo);
                w.Indent++;
                w.WriteLine("ex := new MfException(MfString.Format(MfEnvironment.Instance.GetResourceString(\"Exception_Error\"), A_ThisFunc), e)");
                w.WriteLine(@"ex.SetProp(A_LineFile, A_LineNumber, A_ThisFunc)");
                w.WriteLine(@"throw ex");
                w.Indent--;
                w.WriteLine(bc);
            }
            else
            {
                w.Write(@"obj");
                w.Write(f);
                w.Write(@" := new ");
                if (PropertyTypeName == "Object")
                {
                    w.Write(ElementName);
                }
                else
                {
                    w.Write(PropertyTypeName);
                }
                w.Write(@"(");
                w.Write(@"mfs");
                w.Write(f);
                w.WriteLine(@")");

                w.Write(@"this.");
                w.Write(FieldName);
                w.Write(@".Add(");
                w.Write(@"obj");
                w.Write(f);
                w.WriteLine(@")");

            }
           
           
           

            

            w.Indent--;
            w.WriteLine(bc);

           

            w.Indent--;
            w.WriteLine(bc);

            w.WriteLine();
#if DEBUG
            w.WriteLineNoTabs("; End : WriteParseXmlPropertyArrayItem()");
#endif
        }

        // private void WriteParseXmlPropertyListElement(string ClassName, string FieldName, string PropertyName, string PropertyTypeName)
        private void WriteParseXmlPropertyListElement(Type t, string FieldName, PropertyInfo prop, XmlElementAttribute attrib)
        {
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteParseXmlPropertyListElement()");
#endif
            string f = Helper.UpperCaseFirst(FieldName);
            string PropertyTypeName = attrib.ElementName;
            string ClassName = t.Name;
            if (this.m_SpDic.ContainsKey(t.Name))
            {
                ClassName = this.m_SpDic[t.Name].ElementName;
            }
           

            w.Write(@"iCount := XmlHelper.xpath(xml, {0}/", q);
            w.Write(ClassName);
            //w.Write(PropertyTypeName);
            w.Write(@"/");
            w.Write(PropertyTypeName);
            w.WriteLine(@"/Count(){0})", q);

            w.WriteLine(@"Loop,%iCount%");
            w.WriteLine(bo);
            w.Indent++;

           

            w.Write(@"s");
            w.Write(f);
            w.Write(@" := XmlHelper.xpath(xml, MfString.Format({0}/", q);
            w.Write(ClassName);
            w.Write(@"/");
            w.Write(PropertyTypeName);
            w.Write(@"[{0}]");
            w.WriteLine(@"{0}, A_Index))", q);

            //w.Write(@"mfs");
            //w.Write(f);
            //w.WriteLine(@".Trim()");

            //w.Write(@"if (mfs");
            //w.Write(f);
            //w.WriteLine(@".Length > 0)");
            //w.WriteLine(bo);
            //w.Indent++;


            w.Write(@"obj");
            w.Write(f);
            w.Write(@" := new ");
            w.Write(prop.PropertyType.Name.Substring(0, prop.PropertyType.Name.Length - 2));
            // w.Write(PropertyTypeName);
            w.Write(@"(");
            w.Write(@"s");
            w.Write(f);
            w.WriteLine(@")");

            w.Write(@"this.");
            w.Write(FieldName);
            w.Write(@".Add(");
            w.Write(@"obj");
            w.Write(f);
            w.WriteLine(@")");

            //w.Indent--;
            //w.WriteLine(bc);

            w.Indent--;
            w.WriteLine(bc);

            w.WriteLine();
#if DEBUG
            w.WriteLineNoTabs("; End : WriteParseXmlPropertyListElement()");
#endif
        }
        #endregion

        #endregion

        #region  Enum
        private void WriteEnum(Type t)
        {
#if DEBUG
            w.WriteLineNoTabs("; Start : WriteEnum()");
#endif
            #region Open Class
            w.Indent = 0;
            w.WriteLine(@"class {0} extends MfEnum", t.Name);
            w.WriteLine(bo);
            w.Indent++;
#endregion

#region Fields
            w.WriteLine(@"static m_Instance := MfNull.Null");
            w.WriteLine();

#endregion

#region Constructor
            
            w.WriteLine(@"__New(args*) {");
            w.Indent++;
           
            w.WriteLine(@"if (this.base.__Class != {0}{1}{0})", q, t.Name);
            w.WriteLine(@"{");
            w.Indent++;
            w.Write(@"throw new MfNotSupportedException(MfEnvironment.Instance.GetResourceString(");
            w.WriteLine(string.Format("{0}NotSupportedException_Sealed_Class{0},{0}{1}{0}))", q, t.Name ));
            w.Indent--;

            w.WriteLine(@"}");
            w.WriteLine(@"base.__New(args*)");
            w.WriteLine(string.Format("this.m_isInherited := this.__Class != {0}{1}{0}", q, t.Name ));

            w.Indent--;
            w.WriteLine("}");
            w.WriteLine();

#endregion

#region AddEnums
            w.WriteLine("AddEnums() {");
            w.Indent++;

            FieldInfo FirstEnum = null;
            var enumName = t.Name;
            foreach (var fieldInfo in t.GetFields())
            {
                if (fieldInfo.FieldType.IsEnum)
                {
                    if (FirstEnum == null)
                    {
                        FirstEnum = fieldInfo;
                    }
                    var fName = fieldInfo.Name;
                    var fValue = fieldInfo.GetRawConstantValue();
                    w.WriteLine("this.AddEnumValue({0}{1}{0}, {2})",q, fName, fValue.ToString() );
                }
            }
                      
            w.Indent--;
            w.WriteLine("}");
            w.WriteLine();

#endregion

#region Equals
            /*
             * equal not needed inherit from MfObject
            w.WriteLine("Equals(objA, ObjB = {0}{0})", q);
            w.WriteLine("{");
            w.Indent++;
            w.WriteLine("return base.Equals(objA, ObjB)");
            w.Indent--;
            w.WriteLine("}");
            w.WriteLine();
            */

#endregion

#region GetInstance
            w.WriteLine("GetInstance() {");
            w.Indent++;
            w.WriteLine(@"if (MfNull.IsNull({0}.m_Instance))", t.Name);
            w.WriteLine(@"{");
            w.Indent++;
            if (FirstEnum == null)
            {
                w.WriteLine("{0}.m_Instance := new {0}(0)", t.Name);
            }
            else
            {
                w.WriteLine("{0}.m_Instance := new {0}({1})",t.Name, FirstEnum.GetRawConstantValue().ToString());
            }
            w.Indent--;
            w.WriteLine(@"}");
            w.WriteLine("return {0}.m_Instance", t.Name);


            w.Indent--;
            w.WriteLine("}");
            w.WriteLine();

#endregion

#region GetType
            /*
             * Inherited from MfObject
            w.WriteLine("GetType()");
            w.WriteLine("{");

            w.Indent++;
            w.WriteLine("return base.GetType()");
            w.Indent--;

            w.WriteLine("}");
            w.WriteLine();
            */
#endregion

#region IS
            /*
             * Inherited From MfObject
            w.WriteLine(@"Is(type)");
            w.WriteLine(@"{");

            w.Indent++;
            w.WriteLine(@"typeName := null");
            w.WriteLine(@"if (IsObject(type))");
            w.WriteLine(@"{");
            w.Indent++;
            w.WriteLine(@"if (MfObject.IsObjInstance(type, {0}MfType{0}))", q);
            w.WriteLine(@"{");

            w.Indent++;
            w.WriteLine(@"typeName := type.ClassName");
            w.Indent--;
            w.WriteLine(@"}");
            w.WriteLine(@"else if (type.__Class)");
            w.WriteLine(@"{");
            w.Indent++;
            w.WriteLine(@"typeName := type.__Class");
            w.Indent--;
            w.WriteLine(@"}");
            w.WriteLine(@"else if (type.base.__Class)");
            w.WriteLine(@"{");
            w.Indent++;
            w.WriteLine(@"typeName := type.base.__Class");
            w.Indent--;
            w.WriteLine("}");

            w.Indent--;
            w.WriteLine(@"}");
            w.WriteLine(@"else if (type ~= {0}^[a-zA-Z0-9.]+${0})", q);
            w.WriteLine(@"{");
            w.Indent++;
            w.WriteLine(@"typeName := type");
            w.Indent--;
            w.WriteLine("}");

            w.WriteLine(@"if (typeName = {0}{1}{0})", q, t.Name);
            w.WriteLine(@"{");
            w.Indent++;
            w.WriteLine(@"return true");
            w.Indent--;
            w.WriteLine("}");
            w.WriteLine(@"return base.Is(type)");
            w.Indent--;
            w.WriteLine(@"}");
            w.WriteLine();
            */
#endregion

#region IsObjInstance
            /*
             * Inherited from MfObject
            w.WriteLine(@"IsObjInstance(obj, objType = {0}{0})", q);
            w.WriteLine(@"{");
            w.Indent++;
            w.WriteLine(@"return MfObject.IsObjInstance(obj, objType)");
            w.Indent--;
            w.WriteLine(@"}");
            w.WriteLine();
            */
#endregion

#region ToString
            /*
             * Inherited from MfEnum
            w.WriteLine(@"ToString()");
            w.WriteLine(@"{");
            w.Indent++;
            w.WriteLine(@"return base.ToString()");
            w.Indent--;
            w.WriteLine(@"}");
            w.WriteLine();
            */
#endregion

#region AddAttributes
            if (t.GetCustomAttributes(typeof(FlagsAttribute), false).Length > 0)
            {
                w.WriteLine(@"AddAttributes()");
                w.WriteLine(bo);
                w.Indent++;
                w.Write(@"this.AddAttribute(");
                w.Write(@"new MfFlagsAttribute()");
                w.Write(@")");
                w.Indent--;
                w.WriteLine(bc);
                w.WriteLine();
            }
#endregion

#region Close Class
            w.Indent = 0;
            w.WriteLine(bc);
            w.WriteLine();
            #endregion
#if DEBUG
            w.WriteLineNoTabs("; End : WriteEnum()");
#endif
        }
        #endregion

        #region Helper Functions
        /// <summary>
        /// Gets the field name of a property in a class
        /// </summary>
        /// <param name="pi">The Property to get the field name for</param>
        /// <param name="fDic">A dictionary of the current fields in a class</param>
        /// <returns>
        /// String containing the Field Name if a match is found; Othewise Empty string.
        /// </returns>
        private string GetFieldKey(PropertyInfo pi, ref Dictionary<string, FieldInfo> fDic)
        {
            if (string.IsNullOrWhiteSpace(pi.Name))
            {
                return string.Empty;
            }
            string FieldKey = pi.Name + "Field";

            if (!fDic.ContainsKey(FieldKey))
            {
                if (pi.Name.EndsWith(@"Specified"))
                {
                    FieldKey = pi.Name.Substring(0, pi.Name.Length - 9) + "FieldSpecified";
                }
                //FieldKey = prop.Name + "Specified" + "Field";
            }
            if (!fDic.ContainsKey(FieldKey))
            {
                // sometime the field convert the first letter of the property to upper
                // such as wihen the property name is value. Convert first char of
                // property name back to lower and test the key again
                FieldKey = char.ToLower(pi.Name[0]) + pi.Name.Substring(1);
                FieldKey += "Field";
            }
            if (!fDic.ContainsKey(FieldKey))
            {
                return string.Empty;
            }
            return FieldKey;
        }

       
#endregion

#region Overrides
        public override string ToString()
        {
            return this.baseTextWriter.ToString();
        }
#endregion
    }
}
