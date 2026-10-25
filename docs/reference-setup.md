# 原本の配置

`FlexRenamer/` はFlexible Renamer v8.4（作者Naru）の参照用配布物を置くディレクトリで、Gitには含めない。新しいチェックアウトでは、手元の参照コピーを別途配置する。

1. リポジトリ直下の `FlexRenamer/` に配布物をコピーする。exeは `FlexRenamer/Flexible Renamer.exe`、DLLは `FlexRenamer/StrFunc/StrFunc.dll` になるようにする。
2. [reference-files.json](reference-files.json) の22ファイルをそろえ、下記の手順でサイズ・SHA-256を確認する。この一覧は現在採用している参照コピーを記録したもの。アプリはv8.4だが、Strings.ini内のVersionは8.0.0。
3. `dotnet build` と `dotnet test` を実行する。
4. 原本の画面比較を行う場合は `dotnet run --project tools/FrTools -- sandbox` で作業コピーを作る。

アプリのビルドには要らない（言語ファイル・プリセット・タグ定義・スクリプトは `src/ReflexRenamer/Data/` に取り込み済み）。テストは原本のini等を読んで互換性を確かめ、比較ツールはexe／DLLを使うため、Git管理外でも開発時には必要。

原本をこのディレクトリから直接起動しない。設定やログが書き換わるため、原本アプリは `.work/sandbox/original/` から起動する。

## サイズ・ハッシュの確認

リポジトリ直下のPowerShellで実行する。

```powershell
$referenceManifest = Get-Content -LiteralPath docs/reference-files.json -Raw | ConvertFrom-Json
$referenceErrors = @(
    foreach ($entry in $referenceManifest.files) {
        if (-not (Test-Path -LiteralPath $entry.path -PathType Leaf)) {
            "不足: $($entry.path)"
        } elseif ((Get-Item -LiteralPath $entry.path).Length -ne $entry.size) {
            "サイズ不一致: $($entry.path)"
        } elseif ((Get-FileHash -LiteralPath $entry.path -Algorithm SHA256).Hash -ne $entry.sha256) {
            "SHA-256不一致: $($entry.path)"
        }
    }
)
if ($referenceErrors.Count) {
    throw ($referenceErrors -join [Environment]::NewLine)
}
"参照ファイル $($referenceManifest.files.Count) 件が一致しました。"
```

`.gitignore` で `FlexRenamer/` 全体を除外している。コピーした原本は各開発環境で保持する。
