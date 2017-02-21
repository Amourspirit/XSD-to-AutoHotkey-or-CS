# xsdOut Introduction

xsdOut version 0.7.5.0 is a command line tool for converting XSD to AutoHotkey format or to C# format.

Link to demo and example [here](https://amourspirit.github.io/XSD-to-AutoHotkey-or-CS/).

If converting to AutoHotkey format then [Mini-Framework](https://github.com/Amourspirit/Mini-Framework) for AutoHotkey is required.

The latest binary can be download from the Latest_Binary folder. [here](https://github.com/Amourspirit/XSD-to-AutoHotkey-or-CS/raw/master/Latest_Binary/Xsdout.exe) is a link to the binary.  

It is recommended to place the **xsdOut.exe** binary file in your path such as your Windows folder.  

Command line options for xsdOut.exe  

 -c, --classes       Generate Classes for this Schema  
 -p, --xmlhelper     Append XmlHelper Class to AutoHotKey Output  
 -l, --language      (Default: CS) The language to use for the generated code. Choose from 'CS' or 'AHK'  
 -o, --out           The output directory to put files in. The default is the current Directory.  
 -i, --import        If true then import will be included for AutoHotKey Classes.  
 -x, --export        If true then export will be included for AutoHotKey Classes.  
 -w, --whitespace    Ignore whites space when reading xml. Requires import to be true. Applies only to AHK  
 -h, --help          Display help screen.  