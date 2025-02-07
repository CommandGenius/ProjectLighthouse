#nullable enable
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LBPUnion.ProjectLighthouse.Types.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace LBPUnion.ProjectLighthouse.Helpers.Extensions;

// yoinked and adapted from https://stackoverflow.com/a/68641796
public static class RequestExtensions
{
    private static readonly Regex mobileCheck = new
        ("Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    public static bool IsMobile(this HttpRequest request) => mobileCheck.IsMatch(request.Headers[HeaderNames.UserAgent].ToString());

    public static async Task<bool> CheckCaptchaValidity(this HttpRequest request)
    {
        if (ServerSettings.Instance.HCaptchaEnabled)
        {
            bool gotCaptcha = request.Form.TryGetValue("h-captcha-response", out StringValues values);
            if (!gotCaptcha) return false;

            if (!await CaptchaHelper.Verify(values[0])) return false;
        }

        return true;
    }
}