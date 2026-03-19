using System;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Android.App;
using Android.OS;
using Android.Util;

namespace IME.Features.Settings;

internal static class KamiVipVerificationCoordinator
{
    private const string Tag = "KamiVipVerify";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly object SyncRoot = new();

    private static Handler? _handler;
    private static Action? _scheduledAction;
    private static WeakReference<Activity>? _activityRef;
    private static string _ownerTag = string.Empty;
    private static bool _verifyInFlight;
    private static bool _verifyEndpointUnavailableInProcess;

    public static void Start(Activity activity, string ownerTag, Action? onStateUpdated = null)
    {
        if (activity == null)
        {
            return;
        }

        lock (SyncRoot)
        {
            _activityRef = new WeakReference<Activity>(activity);
            _ownerTag = ownerTag ?? activity.GetType().Name;
            _handler ??= new Handler(Looper.MainLooper);
            _scheduledAction = () =>
            {
                if (!TryGetActivity(out Activity? resumedActivity))
                {
                    Stop();
                    return;
                }

                TriggerVerificationIfDue(resumedActivity, $"{_ownerTag}.timer", onStateUpdated);
                _handler?.PostDelayed(_scheduledAction, (long)KamiVipConfig.VerifyInterval.TotalMilliseconds);
            };

            _handler.RemoveCallbacks(_scheduledAction);
            _handler.PostDelayed(_scheduledAction, (long)KamiVipConfig.VerifyInterval.TotalMilliseconds);
        }

        TriggerVerificationIfDue(activity, $"{_ownerTag}.resume", onStateUpdated);
    }

    public static void Stop()
    {
        lock (SyncRoot)
        {
            if (_handler != null && _scheduledAction != null)
            {
                _handler.RemoveCallbacks(_scheduledAction);
            }

            _activityRef = null;
            _scheduledAction = null;
            _ownerTag = string.Empty;
        }
    }

    private static void TriggerVerificationIfDue(Activity activity, string trigger, Action? onStateUpdated)
    {
        if (!KamiVipConfig.HasStoredBinding(activity))
        {
            return;
        }

        if (_verifyEndpointUnavailableInProcess)
        {
            Log.Info(Tag, $"Skip verify because endpoint is marked unavailable in current process. trigger={trigger}");
            return;
        }

        DateTimeOffset? lastVerifyAt = KamiVipConfig.GetLastVerifyAtUtc(activity);
        if (lastVerifyAt.HasValue && (DateTimeOffset.UtcNow - lastVerifyAt.Value) < KamiVipConfig.VerifyInterval)
        {
            return;
        }

        bool shouldStart;
        lock (SyncRoot)
        {
            shouldStart = !_verifyInFlight;
            if (shouldStart)
            {
                _verifyInFlight = true;
            }
        }

        if (!shouldStart)
        {
            return;
        }

        var appContext = activity.ApplicationContext;
        Task.Run(async () =>
        {
            bool stateChanged = false;

            try
            {
                stateChanged = await VerifyAsync(appContext, trigger);
            }
            finally
            {
                lock (SyncRoot)
                {
                    _verifyInFlight = false;
                }
            }

            if (!stateChanged || onStateUpdated == null)
            {
                return;
            }

            if (TryGetActivity(out Activity? currentActivity))
            {
                currentActivity.RunOnUiThread(onStateUpdated);
            }
        });
    }

    private static async Task<bool> VerifyAsync(Android.Content.Context context, string trigger)
    {
        string baseUrl = KamiVipConfig.NormalizeBaseUrl(KamiVipConfig.KamiBaseUrl);
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri))
        {
            Log.Warn(Tag, $"Skip verify because base URL is invalid. trigger={trigger}");
            return false;
        }

        int userId = KamiVipConfig.GetStoredUserId(context);
        string deviceUid = KamiVipConfig.GetOrCreateDeviceUid(context);
        if (userId <= 0 || string.IsNullOrWhiteSpace(deviceUid))
        {
            return false;
        }

        var payload = new VerifyApiRequest
        {
            UserId = userId,
            UserRef = KamiVipConfig.BuildUserRef(context),
            DeviceUid = deviceUid
        };

        string requestJson = JsonSerializer.Serialize(payload, JsonOptions);
        Uri endpoint = new Uri(baseUri, KamiVipConfig.VerifyEndpointPath);

        try
        {
            using var client = CreateHttpClient(baseUri);
            using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await client.PostAsync(endpoint, content);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<VerifyApiResponse>(responseBody, JsonOptions);
                bool isActive = result?.IsActive ?? result?.Active ?? true;
                string vipExpiresAt = result?.VipExpiresAt ?? string.Empty;
                int resolvedUserId = result?.UserId ?? userId;

                KamiVipConfig.MarkVerifiedNow(context);

                if (!isActive)
                {
                    KamiVipConfig.ClearVipState(context);
                    Log.Warn(Tag, $"Remote verify marked VIP inactive. trigger={trigger}");
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(vipExpiresAt))
                {
                    string previous = KamiVipConfig.GetStoredVipExpiresAt(context);
                    KamiVipConfig.SaveVipState(context, resolvedUserId, vipExpiresAt);
                    bool changed = !string.Equals(previous, vipExpiresAt, StringComparison.Ordinal)
                                   || KamiVipConfig.GetStoredUserId(context) != resolvedUserId;
                    Log.Info(Tag, $"Remote verify success. trigger={trigger}, changed={changed}, expiresAt={vipExpiresAt}");
                    return changed;
                }

                Log.Warn(Tag, $"Remote verify succeeded but no vip_expires_at was returned. trigger={trigger}");
                return false;
            }

            if (response.StatusCode == HttpStatusCode.Forbidden
                || response.StatusCode == HttpStatusCode.Conflict
                || response.StatusCode == HttpStatusCode.Gone)
            {
                KamiVipConfig.ClearVipState(context);
                KamiVipConfig.MarkVerifiedNow(context);
                Log.Warn(Tag, $"Remote verify revoked local VIP state. trigger={trigger}, status={(int)response.StatusCode}, body={Truncate(responseBody, 300)}");
                return true;
            }

            if (response.StatusCode == HttpStatusCode.NotFound
                || response.StatusCode == HttpStatusCode.MethodNotAllowed
                || response.StatusCode == HttpStatusCode.NotImplemented)
            {
                _verifyEndpointUnavailableInProcess = true;
                Log.Warn(Tag, $"Verify endpoint appears unavailable. trigger={trigger}, status={(int)response.StatusCode}, url={endpoint}");
                return false;
            }

            Log.Warn(Tag, $"Remote verify failed. trigger={trigger}, status={(int)response.StatusCode}, body={Truncate(responseBody, 300)}");
            return false;
        }
        catch (Exception ex) when (IsTlsTrustAnchorError(ex))
        {
            Log.Warn(Tag, $"Remote verify TLS validation failed. trigger={trigger}, error={ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Remote verify exception. trigger={trigger}, error={ex.Message}");
            return false;
        }
    }

    private static HttpClient CreateHttpClient(Uri baseUri)
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
                    Log.Warn(Tag, $"TLS bypass applied for host={requestUri!.Host}, errors={sslErrors}");
                    return true;
                }

                return false;
            };
        }

        return new HttpClient(handler)
        {
            Timeout = KamiVipConfig.VerifyTimeout
        };
    }

    private static bool TryGetActivity(out Activity? activity)
    {
        activity = null;
        return _activityRef != null && _activityRef.TryGetTarget(out activity) && activity != null && !activity.IsFinishing;
    }

    private static bool IsTlsTrustAnchorError(Exception ex)
    {
        for (Exception? current = ex; current != null; current = current.InnerException)
        {
            string text = current.ToString();
            if (text.IndexOf("Trust anchor for certification path not found", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf(nameof(AuthenticationException), StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("SSLHandshakeException", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("CertPathValidatorException", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text ?? string.Empty;
        }

        return text.Substring(0, maxLength);
    }

    private sealed class VerifyApiRequest
    {
        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("user_ref")]
        public string UserRef { get; set; } = string.Empty;

        [JsonPropertyName("device_uid")]
        public string DeviceUid { get; set; } = string.Empty;
    }

    private sealed class VerifyApiResponse
    {
        [JsonPropertyName("user_id")]
        public int? UserId { get; set; }

        [JsonPropertyName("vip_expires_at")]
        public string? VipExpiresAt { get; set; }

        [JsonPropertyName("is_active")]
        public bool? IsActive { get; set; }

        [JsonPropertyName("active")]
        public bool? Active { get; set; }
    }
}
