# WXballoon for Windows
実行しダイアログに郵便番号を入力すると、今日と明日の天気がバルーン通知されます。  
表示秒数は10秒を指定していますが、基本的にはWindowsの設定に依存します。  
  
起動時に表示されるコマンドプロンプトがわずわらしい場合は、exeのショートカットを作成し、プロパティで実行時の大きさを「最小化」にしてください。  
コマンドプロンプトには、デバッグ情報が表示されます。  
  
引数に郵便番号を渡せば、郵便番号を尋ねるダイアログが表示されません。  
毎回同じ郵便番号を使用する場合は、ショートカットの引数に郵便番号を追加してください。  

```bat
WXballoon.exe 1234567
```
  
下記のAPIを使用しています。(ご提供ありがとうございます)  
- 郵便番号からの住所取得 [postal-code-api](https://github.com/madefor/postal-code-api)  
- 郵便番号からの緯度経度取得 [Nominatim](https://nominatim.org/)  
- 緯度経度からの天気予報取得 [WeatherAPI.com](https://www.weatherapi.com/) 
  
実行時に、exeと同じ場所にjsonファイルを作成します。 
郵便番号と緯度経度は30日間、天気情報は1時間の間、キャッシュを参照します。  
新規に取得したい場合は、jsonファイルを削除して実行してください。
  
[1.0](https://github.com/thrashem/WXballoon/Release/tag/1.0) 新規リリース
