param([Parameter(Mandatory=$true)][string]$ReferenceDirectory)
$ErrorActionPreference = 'Stop'
$ReferenceDirectory = (Resolve-Path -LiteralPath $ReferenceDirectory).Path
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
public static class StrFuncInspect {
  [DllImport("oleaut32", CharSet=CharSet.Unicode, PreserveSig=false)]
  static extern void LoadTypeLibEx(string path, int kind, out ITypeLib library);
  public static string Dump(string path) {
    ITypeLib lib; LoadTypeLibEx(path, 2, out lib);
    var output = new StringBuilder();
    IntPtr lp;lib.GetLibAttr(out lp);var la=(TYPELIBATTR)Marshal.PtrToStructure(lp,typeof(TYPELIBATTR));output.AppendLine("Library "+la.guid+" "+la.wMajorVerNum+"."+la.wMinorVerNum);lib.ReleaseTLibAttr(lp);
    for(int i=0;i<lib.GetTypeInfoCount();i++) {
      ITypeInfo type; lib.GetTypeInfo(i,out type);
      IntPtr ptr; type.GetTypeAttr(out ptr);
      var attr=(TYPEATTR)Marshal.PtrToStructure(ptr,typeof(TYPEATTR));
      string name,doc,help; int hc; type.GetDocumentation(-1,out name,out doc,out hc,out help);
      output.AppendLine(name+" "+attr.typekind+" "+attr.guid);
      for(int n=0;n<attr.cVars;n++) {
        IntPtr vp; type.GetVarDesc(n,out vp);
        var v=(VARDESC)Marshal.PtrToStructure(vp,typeof(VARDESC));
        type.GetDocumentation(v.memid,out name,out doc,out hc,out help);
        output.AppendLine("  "+name+"="+(v.varkind==VARKIND.VAR_CONST?Marshal.GetObjectForNativeVariant(v.desc.lpvarValue):null));
        type.ReleaseVarDesc(vp);
      }
      for(int n=0;n<attr.cFuncs;n++) {
        IntPtr fp; type.GetFuncDesc(n,out fp);
        var f=(FUNCDESC)Marshal.PtrToStructure(fp,typeof(FUNCDESC));
        var names=new string[f.cParams+1];int count;type.GetNames(f.memid,names,names.Length,out count);
        output.AppendLine("  "+f.memid+" "+f.invkind+" "+String.Join(",",names)+" optional="+f.cParamsOpt);
        type.ReleaseFuncDesc(fp);
      }
      type.ReleaseTypeAttr(ptr);Marshal.ReleaseComObject(type);
    }
    Marshal.ReleaseComObject(lib);return output.ToString();
  }
}
'@
[StrFuncInspect]::Dump((Join-Path $ReferenceDirectory 'StrFunc.dll'))
