using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Debugger = System.Diagnostics.Debugger;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using IME.Shared.Security;

namespace IME.Features.Settings
{
    [Activity(Label = "卡密会员", Theme = "@style/MyNoActionBarTheme", Exported = true)]
    public class KamiVipActivity : SecurityMonitoredActivity
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private EditText _editKamiCode;
        private Button _btnRedeemCode;
        private TextView _tvVipStatus;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            if (!EnsureSecurityAllowedNow())
            {
                return;
            }
            SetContentView(BuildContentView());
            RefreshVipStatusText();
        }

        protected override void OnResume()
        {
            base.OnResume();
            KamiVipVerificationCoordinator.Start(this, nameof(KamiVipActivity), RefreshVipStatusText);
        }

        protected override void OnPause()
        {
            KamiVipVerificationCoordinator.Stop();
            base.OnPause();
        }

        private View BuildContentView()
        {
            var scroll = new ScrollView(this);
            var root = new LinearLayout(this)
            {
                Orientation = Orientation.Vertical
            };
            root.SetPadding(DpToPx(16), DpToPx(16), DpToPx(16), DpToPx(16));

            var title = new TextView(this)
            {
                Text = "卡密会员兑换"
            };
            title.SetTextSize(Android.Util.ComplexUnitType.Sp, 22);
            title.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent)
            {
                BottomMargin = DpToPx(12)
            };

            _editKamiCode = new EditText(this)
            {
                Hint = "输入卡密"
            };
            _editKamiCode.InputType = Android.Text.InputTypes.ClassText;
            _editKamiCode.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                DpToPx(48))
            {
                BottomMargin = DpToPx(12)
            };

            _btnRedeemCode = new Button(this)
            {
                Text = "兑换卡密"
            };
            _btnRedeemCode.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                DpToPx(48))
            {
                BottomMargin = DpToPx(12)
            };
            _btnRedeemCode.Click += async (_, _) => await RedeemCodeAsync();

            _tvVipStatus = new TextView(this)
            {
                Text = "会员状态：未激活"
            };
            _tvVipStatus.SetTextSize(Android.Util.ComplexUnitType.Sp, 16);

            root.AddView(title);
            root.AddView(_editKamiCode);
            root.AddView(_btnRedeemCode);
            root.AddView(_tvVipStatus);

            scroll.AddView(root);
            return scroll;
        }

        private async Task RedeemCodeAsync()
        {
            string code = _editKamiCode?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                ShowToast("请输入卡密");
                return;
            }

            string baseUrl = KamiVipConfig.NormalizeBaseUrl(KamiVipConfig.KamiBaseUrl);
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri))
            {
                ShowToast("卡密地址无效");
                return;
            }

            string deviceUid = KamiVipConfig.GetOrCreateDeviceUid(this);
            string userRef = KamiVipConfig.BuildUserRef(this);

            var payload = new RedeemApiRequest
            {
                Code = code,
                UserRef = userRef,
                DeviceUid = deviceUid
            };

            string requestJson = JsonSerializer.Serialize(payload, JsonOptions);
            Uri endpoint = new Uri(baseUri, KamiVipConfig.RedeemEndpointPath);

            _btnRedeemCode.Enabled = false;
            _btnRedeemCode.Text = "兑换中...";

            try
            {
                using var client = CreateKamiHttpClient(baseUri);
                using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                using HttpResponseMessage response = await client.PostAsync(endpoint, content);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<RedeemApiResponse>(responseBody, JsonOptions);
                    if (result == null || result.UserId <= 0 || string.IsNullOrWhiteSpace(result.VipExpiresAt))
                    {
                        ShowToast("兑换成功，但返回数据不完整");
                        return;
                    }

                    SaveKamiRedeemResult(result.UserId.Value, result.VipExpiresAt);
                    RefreshVipStatusText();
                    _editKamiCode.Text = string.Empty;
                    ShowToast("卡密兑换成功");
                    return;
                }

                string message = ExtractServerMessage(responseBody);
                LogRedeemFailure(endpoint, response.StatusCode, message, responseBody);
#if DEBUG
                BreakOnRedeemFailureForDebug(response.StatusCode, message, responseBody);
#endif
                ShowToast(MapRedeemErrorMessage(response.StatusCode, message));
            }
            catch (TaskCanceledException ex)
            {
                LogRedeemException(ex);
                ShowToast("请求超时，请稍后重试");
            }
            catch (HttpRequestException ex) when (IsTlsTrustAnchorError(ex))
            {
                LogRedeemException(ex);
#if DEBUG
                BreakOnExceptionForDebug();
#endif
                ShowToast("SSL证书校验失败：服务器证书链不受信任");
            }
            catch (Exception ex)
            {
                LogRedeemException(ex);
#if DEBUG
                BreakOnExceptionForDebug();
#endif
                ShowToast("兑换失败：" + ex.Message);
            }
            finally
            {
                _btnRedeemCode.Enabled = true;
                _btnRedeemCode.Text = "兑换卡密";
            }
        }

        private void SaveKamiRedeemResult(int userId, string vipExpiresAt)
        {
            KamiVipConfig.SaveVipState(this, userId, vipExpiresAt);
            KamiVipConfig.MarkVerifiedNow(this);
        }

        private void RefreshVipStatusText()
        {
            int userId = KamiVipConfig.GetStoredUserId(this);
            string vipExpiresAt = KamiVipConfig.GetStoredVipExpiresAt(this);

            if (string.IsNullOrWhiteSpace(vipExpiresAt))
            {
                _tvVipStatus.Text = "会员状态：未激活";
                return;
            }

            if (!DateTimeOffset.TryParse(
                    vipExpiresAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset expiresAt))
            {
                _tvVipStatus.Text = $"会员到期：{vipExpiresAt}";
                return;
            }

            string localText = expiresAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            if (expiresAt <= DateTimeOffset.UtcNow)
            {
                _tvVipStatus.Text = $"会员状态：已过期（到期时间 {localText}）";
                return;
            }

            _tvVipStatus.Text = userId > 0
                ? $"会员状态：已激活（用户ID {userId}，到期 {localText}）"
                : $"会员状态：已激活（到期 {localText}）";
        }

        private static HttpClient CreateKamiHttpClient(Uri baseUri)
        {
            var handler = new HttpClientHandler();

            if (KamiVipConfig.AllowUntrustedTlsForKamiHost)
            {
                handler.ServerCertificateCustomValidationCallback = (request, _, _, sslErrors) =>
                {
                    if (sslErrors == System.Net.Security.SslPolicyErrors.None)
                    {
                        return true;
                    }

                    Uri? requestUri = request?.RequestUri;
                    bool sameHost = requestUri != null &&
                                    string.Equals(requestUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase);

                    if (sameHost)
                    {
                        Log.Warn("KamiVip", $"TLS bypass applied for host={requestUri!.Host}, errors={sslErrors}");
                        return true;
                    }

                    return false;
                };
            }

            return new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
        }

        private static string MapRedeemErrorMessage(HttpStatusCode statusCode, string serverMessage)
        {
            return statusCode switch
            {
                HttpStatusCode.NotFound => "卡密不存在或用户不存在",
                HttpStatusCode.Conflict => "卡密已使用或设备/商户不匹配",
                HttpStatusCode.Gone => "卡密已过期",
                HttpStatusCode.Forbidden => string.IsNullOrWhiteSpace(serverMessage)
                    ? "商户或管理员已禁用"
                    : "商户或管理员已禁用：" + serverMessage,
                HttpStatusCode.UnprocessableEntity => string.IsNullOrWhiteSpace(serverMessage) ? "参数校验失败" : serverMessage,
                HttpStatusCode.TooManyRequests => "请求过于频繁，请稍后重试",
                _ => string.IsNullOrWhiteSpace(serverMessage)
                    ? $"兑换失败（HTTP {(int)statusCode}）"
                    : serverMessage
            };
        }

        private void LogRedeemFailure(Uri endpoint, HttpStatusCode statusCode, string message, string rawBody)
        {
            string body = Truncate(rawBody, 500);
            Log.Warn("KamiVip", $"Redeem failed | status={(int)statusCode} | url={endpoint} | message={message} | body={body}");
        }

        private void LogRedeemException(Exception ex)
        {
            Log.Error("KamiVip", $"Redeem exception: {ex}");
        }

#if DEBUG
        private static void BreakOnRedeemFailureForDebug(HttpStatusCode statusCode, string serverMessage, string rawBody)
        {
            if (!Debugger.IsAttached)
            {
                return;
            }

            if (statusCode == HttpStatusCode.Forbidden || ContainsForbiddenKeywords(serverMessage) || ContainsForbiddenKeywords(rawBody))
            {
                Debugger.Break();
            }
        }

        private static void BreakOnExceptionForDebug()
        {
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
        }

        private static bool ContainsForbiddenKeywords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalized = text.ToLowerInvariant();
            return normalized.Contains("禁言")
                || normalized.Contains("禁用")
                || normalized.Contains("forbidden")
                || normalized.Contains("disabled")
                || normalized.Contains("muted")
                || normalized.Contains("banned");
        }
#endif

        private static string ExtractServerMessage(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return string.Empty;
            }

            try
            {
                var error = JsonSerializer.Deserialize<RedeemApiErrorResponse>(body, JsonOptions);
                if (!string.IsNullOrWhiteSpace(error?.Message))
                {
                    return error.Message;
                }

                if (error?.Errors != null)
                {
                    string firstError = error.Errors.Values.FirstOrDefault(v => v != null && v.Count > 0)?.FirstOrDefault() ?? string.Empty;
                    return firstError;
                }
            }
            catch
            {
                // ignore parse error and fallback
            }

            return body.Length > 120 ? body.Substring(0, 120) : body;
        }

        private static bool IsTlsTrustAnchorError(Exception ex)
        {
            for (Exception? current = ex; current != null; current = current.InnerException)
            {
                string text = current.ToString();
                if (text.IndexOf("Trust anchor for certification path not found", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("SSLHandshakeException", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("CertPathValidatorException", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (text.Length <= maxLength)
            {
                return text;
            }

            return text.Substring(0, maxLength);
        }

        private void ShowToast(string message)
        {
            Toast.MakeText(this, message, ToastLength.Short)?.Show();
        }

        private int DpToPx(int dp)
        {
            float density = Resources?.DisplayMetrics?.Density ?? 1f;
            return (int)Math.Round(dp * density);
        }

        private sealed class RedeemApiRequest
        {
            [JsonPropertyName("code")]
            public string Code { get; set; } = string.Empty;

            [JsonPropertyName("user_ref")]
            public string UserRef { get; set; } = string.Empty;

            [JsonPropertyName("device_uid")]
            public string DeviceUid { get; set; } = string.Empty;
        }

        private sealed class RedeemApiResponse
        {
            [JsonPropertyName("user_id")]
            public int? UserId { get; set; }

            [JsonPropertyName("vip_expires_at")]
            public string? VipExpiresAt { get; set; }
        }

        private sealed class RedeemApiErrorResponse
        {
            [JsonPropertyName("message")]
            public string? Message { get; set; }

            [JsonPropertyName("errors")]
            public Dictionary<string, List<string>>? Errors { get; set; }
        }
    }
}
