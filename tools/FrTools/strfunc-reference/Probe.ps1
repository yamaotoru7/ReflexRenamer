# 原本の StrFunc.dll（32bit COM）を登録せずに生成し、cases.txt の VBScript の式を ScriptControl で評価して JSON に書き出す。
# 32bit Windows PowerShell で実行する（docs/progress.md の T116 節を参照）。式の型は TypeName、エラーは番号と説明を記録する。
# 原本のプロセスごと落ちる式（%n、%s に数値など）は cases.txt でコメントにしてある。
param(
  [Parameter(Mandatory=$true)][string]$ReferenceDirectory,
  [string]$Cases,
  [string]$Output
)
$ErrorActionPreference = 'Stop'
$ReferenceDirectory = (Resolve-Path -LiteralPath $ReferenceDirectory).Path
if (-not $Cases) { $Cases = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'cases.txt' }
if (-not $Output) { $Output = Join-Path $ReferenceDirectory 'sample.json' }
if ([IntPtr]::Size -ne 4) { throw '32bit の PowerShell で実行してください。' }
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class StrFuncFactory {
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct ActCtx {
    public int Size,Flags;public string Source;public ushort Architecture,Language;public string Directory,Resource,Application;public IntPtr Module;
  }
  [DllImport("kernel32",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateActCtx(ref ActCtx context);
  [DllImport("kernel32",SetLastError=true)] static extern bool ActivateActCtx(IntPtr context,out UIntPtr cookie);
  [DllImport("kernel32",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibrary(string path);
  [DllImport("kernel32",CharSet=CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr library,string name);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetClass(ref Guid clsid,ref Guid iid,out IntPtr result);
  [ComImport,Guid("00000001-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  interface IClassFactory {
    void CreateInstance(IntPtr outer,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out object result);
    void LockServer([MarshalAs(UnmanagedType.Bool)]bool value);
  }
  public static object Create(string path) {
    var context=new ActCtx();context.Size=Marshal.SizeOf(typeof(ActCtx));context.Source=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path),"StrFunc.manifest");
    IntPtr handle=CreateActCtx(ref context);if(handle==new IntPtr(-1))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    UIntPtr cookie;if(!ActivateActCtx(handle,out cookie))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    IntPtr library=LoadLibrary(path);
    if(library==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    var get=(GetClass)Marshal.GetDelegateForFunctionPointer(GetProcAddress(library,"DllGetClassObject"),typeof(GetClass));
    Guid clsid=new Guid("4fcd6060-957f-476a-b8da-a4a44d06e594"), factoryId=new Guid("00000001-0000-0000-C000-000000000046");
    IntPtr pointer;Marshal.ThrowExceptionForHR(get(ref clsid,ref factoryId,out pointer));
    var factory=(IClassFactory)Marshal.GetObjectForIUnknown(pointer);Marshal.Release(pointer);
    Guid dispatch=new Guid("00020400-0000-0000-C000-000000000046");object obj;
    factory.CreateInstance(IntPtr.Zero,ref dispatch,out obj);Marshal.ReleaseComObject(factory);return obj;
  }
}
'@
$f = [StrFuncFactory]::Create((Join-Path $ReferenceDirectory 'StrFunc.dll'))
$sc = New-Object -ComObject MSScriptControl.ScriptControl
$sc.Language = 'VBScript'
$sc.AddObject('f', $f, $false)
$sc.ExecuteStatement('vs = "abc" : vn = 5 : vd = #2003-01-15 10:20:30#')

$results = New-Object System.Collections.ArrayList
foreach ($line in (Get-Content -LiteralPath $Cases -Encoding UTF8)) {
  $expr = $line.Trim()
  if ($expr.Length -eq 0 -or $expr.StartsWith('#')) { continue }
  [Console]::Error.WriteLine($expr) # 原本が落ちたとき、どの式かを標準エラーの最後の行で分かるようにする
  $entry = [ordered]@{ expr = $expr }
  try {
    $type = $sc.Eval("TypeName($expr)")
    $value = $sc.Eval($expr)
    $entry.type = $type
    if ($null -eq $value) { $entry.value = $null } else { $entry.value = [string]$value }
  } catch {
    $entry.error = $sc.Error.Number
    $entry.description = $sc.Error.Description
    $sc.Error.Clear()
  }
  [void]$results.Add([pscustomobject]$entry)
}
$json = ConvertTo-Json -InputObject $results.ToArray() -Depth 3
[System.IO.File]::WriteAllText($Output, $json, (New-Object System.Text.UTF8Encoding($false)))
"$($results.Count) cases -> $Output"
