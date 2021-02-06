# ERBPP

## 何

ERBファイルのインデントを自動でやるやつ。名前的にはプリティプリンタぽいけどそこまでの機能はない。

## 動作環境

Windows x64。作者はWindows 10 (20H2) x64で動かしています。

## 必要なもの

[.NET 5](https://dotnet.microsoft.com/download/dotnet/5.0)

## 使い方

標準入力から読んで、標準出力に書き出すだけ。

```
ERBPP < INPUT.ERB > OUTPUT.ERB
```

## 現状の制限

- 私が口上で使っている関数・変数のみしかチェックしていないので、未使用の関数・変数が含まれたファイルを処理しようとすると例外を吐きます。
  [PR](https://github.com/whitebell/ERBPP/pulls)投げるか[issue](https://github.com/whitebell/ERBPP/issues)立ててくれれば対応します。

- エンコードは入出力ともBOM付UTF-8のみ。これに関してはPRこない限りほかのエンコードへの対応はしません。

## License

MIT。
