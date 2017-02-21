using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BigByteTechnologies.XsdOut
{
    internal class SpecialPropertyDescriptor
    {
        internal SpecialPropertyDescriptor()
        {
            IsArray = false;
        }
        public string ClassName { get; set; }
        public string ElementName { get; set; }
        public string Key { get; set; }
        public bool IsArray { get; set; }
        public string Property { get; set; }
        public string PropertyType { get; set; }
    }
}
