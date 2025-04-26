using System;
using System.Drawing;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;

class Program
{
    private static readonly HttpClient client = new HttpClient();
    private const int FixedDisplayTimeMilliseconds = 10000; // 通知表示時間（10秒）
    private static Dictionary<string, (string location, string titleLocation, double lat, double lon, DateTime timestamp)> locationCache = new Dictionary<string, (string location, string titleLocation, double lat, double lon, DateTime timestamp)>();
    private static Dictionary<string, (WeatherCacheData weatherData, DateTime timestamp)> weatherCache = new Dictionary<string, (WeatherCacheData weatherData, DateTime timestamp)>();
    private static readonly TimeSpan LocationCacheExpiration = TimeSpan.FromDays(30); // 郵便番号キャッシュ：30日
    private static readonly TimeSpan WeatherCacheExpiration = TimeSpan.FromHours(1); // 天気キャッシュ：1時間
    private static readonly string LocationCacheFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WXballoon_postal_cache.json");
    private static readonly string WeatherCacheFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WXballoon_weather_cache.json");
    private const string WeatherApiKey = "WeatherAPI.comで取得したAPIキーを記入"; // WeatherAPI.com APIキー

    [STAThread]
    private static void Main(string[] args)
    {
        // ログ
        Console.WriteLine("【WXballoon Log】");

        // ユーザーエージェント設定
        client.DefaultRequestHeaders.Add("User-Agent", "WXballoon/1.0 (https://github.com/thrashem/WXballoon)");

        // キャッシュ読み込み
        LoadLocationCache();
        LoadWeatherCache();

        // 引数処理（郵便番号）
        string zipcode = args.Length > 0 ? args[0] : null;

        // 郵便番号取得
        if (string.IsNullOrEmpty(zipcode))
        {
            zipcode = PromptZipcode();
        }

        // 郵便番号バリデーション
        if (!IsValidZipcode(zipcode))
        {
            zipcode = TryConvertZipcode(zipcode);
            if (!IsValidZipcode(zipcode))
            {
                Console.WriteLine("[error] 無効な郵便番号です。例: 1000001");
                MessageBox.Show("無効な郵便番号です。例: 1000001", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        // 天気情報取得
        (string balloonMessage, string consoleMessage) weatherInfo;
        try
        {
            weatherInfo = GetWeatherAsync(zipcode).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[error] 天気情報取得に失敗しました: {ex.Message}");
            MessageBox.Show(ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // バルーン通知
        ShowBalloonTip(weatherInfo.balloonMessage, weatherInfo.consoleMessage.Split(' ')[0]); // 住所（市町村まで）をタイトルに

        // コンソール出力
        Console.WriteLine("[info] 最終結果: " + weatherInfo.consoleMessage);
    }

    // 郵便番号キャッシュ読み込み
    private static void LoadLocationCache()
    {
        try
        {
            if (File.Exists(LocationCacheFilePath))
            {
                string json = File.ReadAllText(LocationCacheFilePath);
                locationCache = JsonConvert.DeserializeObject<Dictionary<string, (string location, string titleLocation, double lat, double lon, DateTime timestamp)>>(json) ?? new Dictionary<string, (string location, string titleLocation, double lat, double lon, DateTime timestamp)>();
                Console.WriteLine($"[info] 郵便番号キャッシュ読み込み: {locationCache.Count}件");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[error] 郵便番号キャッシュ読み込み失敗: {ex.Message}");
            locationCache = new Dictionary<string, (string location, string titleLocation, double lat, double lon, DateTime timestamp)>();
        }
    }

    // 天気キャッシュ読み込み
    private static void LoadWeatherCache()
    {
        try
        {
            if (File.Exists(WeatherCacheFilePath))
            {
                string json = File.ReadAllText(WeatherCacheFilePath);
                weatherCache = JsonConvert.DeserializeObject<Dictionary<string, (WeatherCacheData weatherData, DateTime timestamp)>>(json) ?? new Dictionary<string, (WeatherCacheData weatherData, DateTime timestamp)>();
                Console.WriteLine($"[info] 天気キャッシュ読み込み: {weatherCache.Count}件");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[error] 天気キャッシュ読み込み失敗: {ex.Message}");
            weatherCache = new Dictionary<string, (WeatherCacheData weatherData, DateTime timestamp)>();
        }
    }

    // 郵便番号キャッシュ保存
    private static void SaveLocationCache()
    {
        try
        {
            string json = JsonConvert.SerializeObject(locationCache, Formatting.Indented);
            File.WriteAllText(LocationCacheFilePath, json);
            Console.WriteLine($"[info] 郵便番号キャッシュ保存: {locationCache.Count}件");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[error] 郵便番号キャッシュ保存失敗: {ex.Message}");
        }
    }

    // 天気キャッシュ保存
    private static void SaveWeatherCache()
    {
        try
        {
            string json = JsonConvert.SerializeObject(weatherCache, Formatting.Indented);
            File.WriteAllText(WeatherCacheFilePath, json);
            Console.WriteLine($"[info] 天気キャッシュ保存: {weatherCache.Count}件");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[error] 天気キャッシュ保存失敗: {ex.Message}");
        }
    }

    private static bool IsValidZipcode(string zipcode)
    {
        return Regex.IsMatch(zipcode, @"^\d{7}$");
    }

    private static string TryConvertZipcode(string zipcode)
    {
        if (Regex.IsMatch(zipcode, @"^\d{3}-\d{4}$"))
        {
            return zipcode.Replace("-", "");
        }
        return zipcode;
    }

    private static string PromptZipcode()
    {
        Form form = new Form
        {
            Text = "郵便番号の入力",
            Size = new Size(300, 150),
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        Label label = new Label
        {
            Text = "郵便番号（例: 1000001）:",
            Location = new Point(20, 20),
            Size = new Size(250, 20)
        };

        TextBox textBox = new TextBox
        {
            Location = new Point(20, 50),
            Size = new Size(200, 20),
            MaxLength = 7
        };
        textBox.KeyPress += (s, e) =>
        {
            if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
                e.Handled = true;
        };

        Button okButton = new Button
        {
            Text = "OK",
            Location = new Point(20, 80),
            DialogResult = DialogResult.OK
        };

        Button cancelButton = new Button
        {
            Text = "キャンセル",
            Location = new Point(100, 80),
            DialogResult = DialogResult.Cancel
        };

        form.Controls.AddRange(new Control[] { label, textBox, okButton, cancelButton });
        form.AcceptButton = okButton;
        form.CancelButton = cancelButton;

        if (form.ShowDialog() == DialogResult.OK)
        {
            string input = textBox.Text.Trim();
            if (IsValidZipcode(input))
                return input;
            Console.WriteLine("[error] 無効な郵便番号です。7桁の数字を入力してください。例: 1000001");
            MessageBox.Show("無効な郵便番号です。7桁の数字を入力してください。例: 1000001", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return PromptZipcode();
        }
        Environment.Exit(0);
        return null;
    }

    private static async Task<(string balloonMessage, string consoleMessage)> GetWeatherAsync(string zipcode)
    {
        // 郵便番号キャッシュ
        string location, titleLocation;
        double lat, lon;
        if (locationCache.ContainsKey(zipcode))
        {
            var cachedData = locationCache[zipcode];
            if (DateTime.Now - cachedData.timestamp <= LocationCacheExpiration)
            {
                location = cachedData.location;
                titleLocation = cachedData.titleLocation;
                lat = cachedData.lat;
                lon = cachedData.lon;
                Console.WriteLine($"[info] 郵便番号キャッシュ使用: Zipcode={zipcode}, Location={location}, TitleLocation={titleLocation}, Lat={lat}, Lon={lon}, 保存時刻={cachedData.timestamp}");
            }
            else
            {
                Console.WriteLine($"[info] 郵便番号キャッシュ期限切れ: Zipcode={zipcode}, 保存時刻={cachedData.timestamp}");
                locationCache.Remove(zipcode);
                (location, titleLocation, lat, lon) = await FetchLocationAndCoordinates(zipcode);
                locationCache[zipcode] = (location, titleLocation, lat, lon, DateTime.Now);
                SaveLocationCache();
            }
        }
        else
        {
            (location, titleLocation, lat, lon) = await FetchLocationAndCoordinates(zipcode);
            locationCache[zipcode] = (location, titleLocation, lat, lon, DateTime.Now);
            SaveLocationCache();
        }

        // 緯度経度バリデーション
        if (lat < -90 || lat > 90 || lon < -180 || lon > 180)
        {
            Console.WriteLine($"エラー: 無効な緯度経度: Lat={lat}, Lon={lon}");
            throw new Exception($"無効な緯度経度: Lat={lat}, Lon={lon}");
        }

        // 天気キャッシュ
        if (weatherCache.ContainsKey(zipcode))
        {
            var cachedData = weatherCache[zipcode];
            if (DateTime.Now - cachedData.timestamp <= WeatherCacheExpiration)
            {
                Console.WriteLine($"[info] 天気キャッシュ使用: Zipcode={zipcode}, 保存時刻={cachedData.timestamp}");
                return FormatWeatherOutput(cachedData.weatherData, location, titleLocation);
            }
            else
            {
                Console.WriteLine($"[info] 天気キャッシュ期限切れ: Zipcode={zipcode}, 保存時刻={cachedData.timestamp}");
                weatherCache.Remove(zipcode);
            }
        }

        // WeatherAPI.comで天気取得
        string weatherUrl = $"https://api.weatherapi.com/v1/forecast.json?key={WeatherApiKey}&q={lat},{lon}&days=2&lang=ja";
        string weatherResponse;
        try
        {
            HttpResponseMessage response = await client.GetAsync(weatherUrl);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[error] WeatherAPI応答異常: {response.StatusCode}");
                throw new Exception($"WeatherAPI応答異常: {response.StatusCode}");
            }
            weatherResponse = await response.Content.ReadAsStringAsync();
            Console.WriteLine("[data] WeatherAPI Response: " + weatherResponse);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[error] 天気情報取得失敗（ネットワークエラー可能性）: {ex.Message}");
            throw new Exception($"天気情報取得失敗（ネットワークエラー可能性）: {ex.Message}");
        }

        WeatherData weatherData = JsonConvert.DeserializeObject<WeatherData>(weatherResponse);
        if (weatherData?.Forecast?.Forecastday == null || weatherData.Forecast.Forecastday.Count < 2)
        {
            Console.WriteLine("[error] 天気データ解析失敗（データ空または不足）");
            throw new Exception("天気データ解析失敗（データ空または不足）");
        }

        // 天気キャッシュ保存
        var weatherCacheData = new WeatherCacheData
        {
            Today = new WeatherDayData
            {
                Weather = weatherData.Forecast.Forecastday[0].Day.Condition.Text,
                MaxTemp = (int)weatherData.Forecast.Forecastday[0].Day.MaxTempC,
                MinTemp = (int)weatherData.Forecast.Forecastday[0].Day.MinTempC
            },
            Tomorrow = new WeatherDayData
            {
                Weather = weatherData.Forecast.Forecastday[1].Day.Condition.Text,
                MaxTemp = (int)weatherData.Forecast.Forecastday[1].Day.MaxTempC,
                MinTemp = (int)weatherData.Forecast.Forecastday[1].Day.MinTempC
            }
        };
        weatherCache[zipcode] = (weatherCacheData, DateTime.Now);
        SaveWeatherCache();

        return FormatWeatherOutput(weatherCacheData, location, titleLocation);
    }

    private static (string balloonMessage, string consoleMessage) FormatWeatherOutput(WeatherCacheData data, string location, string titleLocation)
    {
        // バルーン通知用（改行）
        string balloonMessage = $"今日：{data.Today.Weather} ({data.Today.MaxTemp}°C/{data.Today.MinTemp}°C)\n" +
                               $"明日：{data.Tomorrow.Weather} ({data.Tomorrow.MaxTemp}°C/{data.Tomorrow.MinTemp}°C)";

        // コンソール用（住所＋スペース区切り）
        string consoleMessage = $"[{location}]の天気 " +
                                $"今日：{data.Today.Weather}({data.Today.MaxTemp}°C/{data.Today.MinTemp}°C) " +
                                $"明日：{data.Tomorrow.Weather}({data.Tomorrow.MaxTemp}°C/{data.Tomorrow.MinTemp}°C)";

        return (balloonMessage, consoleMessage);
    }

    private static async Task<(string location, string titleLocation, double lat, double lon)> FetchLocationAndCoordinates(string zipcode)
    {
        // 郵便番号API
        string part1 = zipcode.Substring(0, 3);
        string part2 = zipcode.Substring(3, 4);
        string apiUrl = $"https://madefor.github.io/postal-code-api/api/v1/{part1}/{part2}.json";

        string postalResponseBody;
        try
        {
            HttpResponseMessage response = await client.GetAsync(apiUrl);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[error] 郵便番号API応答異常: {response.StatusCode}");
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    throw new Exception("郵便番号データが見つかりません（サポート外）");
                throw new Exception($"郵便番号API応答異常: {response.StatusCode}");
            }
            postalResponseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine("[data] Postal Code API Response: " + postalResponseBody);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[error] 郵便番号データ取得失敗: {ex.Message}");
            throw new Exception($"郵便番号データ取得失敗: {ex.Message}");
        }

        PostalData postalData = JsonConvert.DeserializeObject<PostalData>(postalResponseBody);
        if (postalData == null || postalData.data == null || postalData.data.Count == 0)
        {
            Console.WriteLine("[error] 郵便番号から位置情報取得失敗（データ空）");
            throw new Exception("郵便番号から位置情報取得失敗（データ空）");
        }

        var postalItem = postalData.data[0];
        string prefecture = postalItem.ja?.prefecture ?? "不明";
        string city = postalItem.ja?.address1 ?? "な地域";
        string town = postalItem.ja?.address2 ?? "";
        string location = prefecture + city + town; // コンソール用：フル住所
        string titleLocation = prefecture + city; // 通知タイトル用：市町村まで
        if (location == "不明な地域")
        {
            location = "不明な地域";
            titleLocation = "不明な地域";
        }
        Console.WriteLine($"[info] 住所: コンソール={location}, タイトル={titleLocation}");

        // Nominatimで緯度経度取得
        string nominatimUrl = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(location)}&format=json&limit=1";
        string nominatimResponse;
        try
        {
            HttpResponseMessage response = await client.GetAsync(nominatimUrl);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[error] Nominatim API応答異常: {response.StatusCode}");
                throw new Exception($"Nominatim API応答異常: {response.StatusCode}");
            }
            nominatimResponse = await response.Content.ReadAsStringAsync();
            Console.WriteLine("[data] Nominatim API Response: " + nominatimResponse);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[error] 緯度経度取得失敗（ネットワークエラー可能性）: {ex.Message}");
            throw new Exception($"緯度経度取得失敗（ネットワークエラー可能性）: {ex.Message}");
        }

        List<NominatimResult> nominatimData = JsonConvert.DeserializeObject<List<NominatimResult>>(nominatimResponse);
        if (nominatimData == null || nominatimData.Count == 0)
        {
            Console.WriteLine("[error] 住所から緯度経度取得失敗");
            throw new Exception("住所から緯度経度取得失敗");
        }

        double lat = nominatimData[0].lat;
        double lon = nominatimData[0].lon;
        Console.WriteLine($"[info] 緯度経度: Lat={lat}, Lon={lon}");

        return (location, titleLocation, lat, lon);
    }

    private static void ShowBalloonTip(string message, string title)
    {
        using (NotifyIcon notifyIcon = new NotifyIcon())
        {
            try
            {
                notifyIcon.Icon = SystemIcons.Information;
                notifyIcon.Visible = true;
                Console.WriteLine($"[info] バルーン通知開始: タイトル='{title}', メッセージ='{message}'");
                notifyIcon.ShowBalloonTip(FixedDisplayTimeMilliseconds, title, message, ToolTipIcon.Info);
                System.Threading.Thread.Sleep(FixedDisplayTimeMilliseconds);
                Console.WriteLine("[info] バルーン通知終了");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[error] バルーン通知失敗: {ex.Message}");
            }
            finally
            {
                notifyIcon.Visible = false;
            }
        }
    }
}

// WeatherAPI.comデータ構造
public class WeatherData
{
    public Forecast Forecast { get; set; }
}

public class Forecast
{
    public List<ForecastDay> Forecastday { get; set; }
}

public class ForecastDay
{
    public DayData Day { get; set; }
}

public class DayData
{
    [JsonProperty("maxtemp_c")]
    public double MaxTempC { get; set; }
    [JsonProperty("mintemp_c")]
    public double MinTempC { get; set; }
    public Condition Condition { get; set; }
}

public class Condition
{
    public string Text { get; set; }
}

// 天気キャッシュデータ構造
public class WeatherCacheData
{
    public WeatherDayData Today { get; set; }
    public WeatherDayData Tomorrow { get; set; }
}

public class WeatherDayData
{
    public string Weather { get; set; }
    public int MaxTemp { get; set; }
    public int MinTemp { get; set; }
}

// 郵便番号APIデータ構造
public class PostalData
{
    public List<PostalDataItem> data { get; set; }
}

public class PostalDataItem
{
    [JsonProperty("ja")]
    public JaAddress ja { get; set; }
}

public class JaAddress
{
    [JsonProperty("prefecture")]
    public string prefecture { get; set; }
    [JsonProperty("address1")]
    public string address1 { get; set; }
    [JsonProperty("address2")]
    public string address2 { get; set; }
}

// Nominatimデータ構造
public class NominatimResult
{
    [JsonProperty("lat")]
    public double lat { get; set; }
    [JsonProperty("lon")]
    public double lon { get; set; }
}