using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using CineScape;

var root=Path.GetFullPath(args.Length>0?args[0]:".");
int errors=0, files=0, checks=0;
foreach(var file in Directory.GetFiles(Path.Combine(root,"Assets"),"*.cs",SearchOption.AllDirectories))
{
    files++;
    var tree=CSharpSyntaxTree.ParseText(File.ReadAllText(file),new CSharpParseOptions(LanguageVersion.CSharp9));
    foreach(var d in tree.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error)){Console.Error.WriteLine(file+": "+d);errors++;}
}
foreach(var file in Directory.GetFiles(Path.Combine(root,"Assets"),"*.asmdef",SearchOption.AllDirectories).Concat(new[]{Path.Combine(root,"Packages/manifest.json")}))
{using var json=JsonDocument.Parse(File.ReadAllText(file));checks++;}
var temp=Path.Combine(Path.GetTempPath(),"CineScape-static-"+Guid.NewGuid());Directory.CreateDirectory(temp);
try
{
    var path=Path.Combine(temp,"settings.json");
    void Check(bool result,string name){checks++;if(!result){errors++;Console.Error.WriteLine("FAIL "+name);}}
    var s=new SettingsManager(path);Check(s.Data.volume==.8f,"missing defaults");
    s.Data.volume=.27f;s.Data.lastDistance=.72f;s.Save();s.Save();
    var r=new SettingsManager(path);Check(r.Data.volume==.27f&&r.Data.lastDistance==.72f,"atomic replacement roundtrip");
    File.WriteAllText(path,"broken json");r=new SettingsManager(path);Check(r.Data.lastDistance==.5f&&r.LastError!=null,"corrupt fallback");
    File.WriteAllText(path,"{}");r=new SettingsManager(path);Check(r.Data.defaultVolume==.8f&&r.Data.defaultDistance==.5f,"missing fields preserve defaults");
    var data=new AppSettings{volume=float.NaN,lastDistance=9,quality=-5};data.Normalize();Check(data.volume==.8f&&data.lastDistance==1&&data.quality==0,"normalization");
    var nativeDialog=typeof(WindowsFileDialog).GetNestedType("OpenFileName",BindingFlags.NonPublic);
    Check(Marshal.SizeOf(nativeDialog)==(IntPtr.Size==8?152:88),"Win32 dialog ABI size");
    Check(Marshal.OffsetOf(nativeDialog,"file").ToInt64()==(IntPtr.Size==8?48:28),"Win32 dialog file buffer offset");
}
finally{Directory.Delete(temp,true);}
Console.WriteLine($"C# syntax files: {files}; JSON/settings checks: {checks}; errors: {errors}. Unity API compilation/runtime NOT tested.");
return errors==0?0:1;
