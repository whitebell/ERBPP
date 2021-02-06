# ERBPP

## 何

ERBファイルのインデントを自動でやるやつ。名前的にはプリティプリンタぽいけどそこまでの機能はない。

## 使い方

標準入力から読んで、標準出力に書き出すだけ。

```
more FOO.ERB | ERBPP > FOO.INDENT.ERB
```

## 現状の制限

- 私が口上で使っている関数・変数のみしかチェックしていないので、未使用の関数・変数が含まれたファイルを処理しようとすると例外を吐きます。
  [PR](https://github.com/whitebell/ERBPP/pulls)投げるか[issue](https://github.com/whitebell/ERBPP/issues)立ててくれれば対応します。

- エンコードは入出力ともBOM付UTF-8のみ。これに関してはPRこない限りほかのエンコードへの対応はしません。

## License

MIT。
