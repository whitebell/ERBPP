# ERBPP

## 何

ERBファイルのインデントを自動でやるやつ。名前的にはプリティプリンタぽいけどそこまでの機能はない。

## 動作環境

配布バイナリはWindows x64。作者はWindows 10 (21H1) x64で動かしています。
ソースコードからコンパイルしてやれば別に変なことはしてないのでx86でも他OSでも動くはず。

## 必要なもの

[.NET 5](https://dotnet.microsoft.com/download/dotnet/5.0)

## 使い方

標準入力から読んで、標準出力に書き出すだけ。

```
ERBPP < INPUT.ERB > OUTPUT.ERB
```

## 現状の制限

- エンコードは入出力ともBOM付UTF-8のみ。これに関してはPRこない限りほかのエンコードへの対応はしません。

## License

MIT。
