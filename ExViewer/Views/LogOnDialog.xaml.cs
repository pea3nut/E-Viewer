using ExClient;
using ExViewer.Controls;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Opportunity.MvvmUniverse;
using Opportunity.MvvmUniverse.Commands;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// “内容对话框”项模板在 http://go.microsoft.com/fwlink/?LinkId=234238 上进行了说明

namespace ExViewer.Views
{
    public sealed partial class LogOnDialog : MyContentDialog
    {
        public LogOnDialog()
        {
            InitializeComponent();
        }

        private class VMData : ObservableObject
        {
            public VMData()
            {
                LogOn = AsyncCommand.Create(async s =>
                {
                    if (!long.TryParse(MemberId, out var uid))
                        return;
                    var hash = PassHash;
                    var igneous = Igneous;

                    try
                    {
                        await Client.Current.LogOnAsync(uid, hash, igneous);
                    }
                    catch (Exception ex)
                    {
                        Reset();
                        ErrorMsg = ex.Message;
                        ShowErrorMsg = true;
                        Telemetry.LogException(ex);
                        return;
                    }
                    if (!string.IsNullOrEmpty(UserName) && !string.IsNullOrEmpty(Password))
                        AccountManager.CurrentCredential = AccountManager.CreateCredential(UserName, Password);

                    Succeed = true;
                }, s => CanLogOn);
                LogOn.PropertyChanged += (s, e) => OnPropertyChanged(nameof(IsPrimaryButtonEnabled));
            }

            public LogOnInfo LogOnInfoBackup { get; set; }

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private bool _UseCookieLogOn;
            public bool UseCookieLogOn { get => _UseCookieLogOn; set => Set(nameof(PrimaryButtonText), nameof(IsPrimaryButtonEnabled), ref _UseCookieLogOn, value); }

            public AsyncCommand LogOn { get; }

            public string PrimaryButtonText => _UseCookieLogOn
                ? Strings.Resources.Views.LogOnDialog.LogOnButtonText
                : Strings.Resources.Views.LogOnDialog.ResetButtonText;

            public bool IsPrimaryButtonEnabled => _UseCookieLogOn ? CanLogOn && !LogOn.IsExecuting : !LogOn.IsExecuting;

            public bool CanLogOn => !Succeed
                && long.TryParse(MemberId, out _)
                && Regex.IsMatch(PassHash ?? "", @"^[0-9a-fA-F]{32}$")
                && Regex.IsMatch(Igneous ?? "", @"^[0-9a-fA-F]*$");

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private bool _Succeed;
            public bool Succeed
            {
                get => _Succeed;
                set => Set(nameof(CanLogOn), nameof(IsPrimaryButtonEnabled), ref _Succeed, value);
            }

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private string _UserName;
            public string UserName { get => _UserName; set => Set(ref _UserName, value); }

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private string _Password;
            public string Password { get => _Password; set => Set(ref _Password, value); }

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private string _MemberId;
            public string MemberId
            {
                get => _MemberId;
                set => Set(nameof(CanLogOn), nameof(IsPrimaryButtonEnabled), ref _MemberId, value);
            }

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private string _PassHash;
            public string PassHash
            {
                get => _PassHash;
                set => Set(nameof(CanLogOn), nameof(IsPrimaryButtonEnabled), ref _PassHash, value);
            }

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private string _Igneous;
            public string Igneous
            {
                get => _Igneous;
                set => Set(nameof(CanLogOn), nameof(IsPrimaryButtonEnabled), ref _Igneous, value);
            }

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private string _ErrorMsg;
            public string ErrorMsg { get => _ErrorMsg; set => Set(ref _ErrorMsg, value); }

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private bool _WebViewUnavailable;
            public bool WebViewUnavailable { get => _WebViewUnavailable; set => Set(ref _WebViewUnavailable, value); }

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private bool _ShowErrorMsg;
            public bool ShowErrorMsg { get => _ShowErrorMsg; set => Set(ref _ShowErrorMsg, value); }


            public void Reset()
            {
                Succeed = false;

                UserName = null;
                Password = null;

                MemberId = "";
                PassHash = "";
                Igneous = "";
            }
        }

        private readonly VMData VM = new VMData();
        private int resetVersion;
        private ulong navigationId;
        private bool isOpen;
        private bool isResetting;

        public bool Succeed => VM.Succeed;

        private async void btnUseCookie_Click(object sender, RoutedEventArgs e)
        {
            VM.UseCookieLogOn = true;
            await reset();
        }

        private async void btnUseWebpage_Click(object sender, RoutedEventArgs e)
        {
            VM.UseCookieLogOn = false;
            await reset();
        }

        private async Task reset()
        {
            var version = ++resetVersion;
            VM.Reset();
            VM.ShowErrorMsg = false;
            VM.WebViewUnavailable = false;
            wv.CoreWebView2?.Stop();
            wv.CoreWebView2?.Navigate("about:blank");
            if (VM.UseCookieLogOn)
                return;

            isResetting = true;
            try
            {
                await wv.EnsureCoreWebView2Async();
                if (!isOpen || version != resetVersion)
                    return;

                wv.CoreWebView2.Navigate("about:blank");
                var cookies = await wv.CoreWebView2.CookieManager.GetCookiesAsync(Client.LogOnUri.ToString());
                if (!isOpen || version != resetVersion)
                    return;
                foreach (var cookie in cookies)
                    wv.CoreWebView2.CookieManager.DeleteCookie(cookie);

                isResetting = false;
                wv.CoreWebView2.Navigate(Client.LogOnUri.ToString());
            }
            catch (Exception ex)
            {
                if (!isOpen || version != resetVersion)
                    return;
                VM.WebViewUnavailable = wv.CoreWebView2 is null;
                showError(VM.WebViewUnavailable
                    ? Strings.Resources.Views.LogOnDialog.WebViewUnavailableMessage
                    : ex.Message);
                Telemetry.LogException(ex);
            }
            finally
            {
                if (version == resetVersion)
                    isResetting = false;
            }
        }

        private async Task injectLogOnPage(string source)
        {
            var pass = AccountManager.CurrentCredential;
            var u = "";
            var p = "";
            if (pass != null)
            {
                pass.RetrievePassword();
                u = pass.UserName;
                p = pass.Password;
            }
            await wv.ExecuteScriptAsync($@"
(function ()
{{
    if (location.href !== {JsonConvert.SerializeObject(source)}) return;
    var nL = document.LOGIN;
    if(!nL) return;
    var nU = nL.UserName;
    if(!nU) return;
    var nP = nL.PassWord;
    if(!nP) return;
    nU.value = {JsonConvert.SerializeObject(u)};
    nP.value = {JsonConvert.SerializeObject(p)};
    nL.onsubmit = function(ev)
    {{
        var ret = ValidateForm();
        if (ret)
        {{
            window.chrome.webview.postMessage([nU.value, nP.value]);
        }}
        return ret;
    }}
}})();
");
        }

        private async Task readLogOnCookies(int version, ulong navigation)
        {
            if (!isOpen || version != resetVersion || navigation != navigationId
                || VM.UseCookieLogOn || VM.LogOn.IsExecuting || VM.Succeed)
                return;

            var cookies = await wv.CoreWebView2.CookieManager.GetCookiesAsync(Client.LogOnUri.ToString());
            if (!isOpen || version != resetVersion || navigation != navigationId
                || VM.UseCookieLogOn || VM.LogOn.IsExecuting || VM.Succeed)
                return;

            VM.MemberId = cookies.FirstOrDefault(c => c.Name == "ipb_member_id")?.Value;
            VM.PassHash = cookies.FirstOrDefault(c => c.Name == "ipb_pass_hash")?.Value;
            VM.Igneous = "";
            if (VM.CanLogOn)
            {
                wv.CoreWebView2.Stop();
                VM.LogOn.Execute();
            }
        }

        private async void cd_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            isOpen = true;
            if (Client.Current.NeedLogOn)
                CloseButtonText = Strings.Resources.General.Exit;
            else
                CloseButtonText = Strings.Resources.General.Cancel;

            VM.LogOnInfoBackup = Client.Current.GetLogOnInfo();
            Client.Current.ClearLogOnInfo();

            await reset();
        }

        private void wv_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
        {
            if (sender.CoreWebView2 is null)
                return;
            sender.CoreWebView2.Settings.AreHostObjectsAllowed = false;
            sender.CoreWebView2.DOMContentLoaded += wv_DOMContentLoaded;
        }

        private static bool isForumUri(string source)
            => Uri.TryCreate(source, UriKind.Absolute, out var uri)
            && uri.Scheme == Client.ForumsUri.Scheme
            && uri.Host == Client.ForumsUri.Host
            && uri.Port == Client.ForumsUri.Port;

        private static bool isLogOnUri(string source)
            => isForumUri(source)
            && new Uri(source).AbsolutePath == Client.LogOnUri.AbsolutePath
            && new Uri(source).Query.TrimStart('?').Split('&').Contains("act=Login");

        private async void wv_DOMContentLoaded(CoreWebView2 sender, CoreWebView2DOMContentLoadedEventArgs args)
        {
            if (!isOpen || isResetting || VM.UseCookieLogOn || args.NavigationId != navigationId || !isForumUri(sender.Source))
                return;

            var version = resetVersion;
            try
            {
                if (isLogOnUri(sender.Source))
                    await injectLogOnPage(sender.Source);
                await readLogOnCookies(version, args.NavigationId);
            }
            catch (Exception ex)
            {
                if (isOpen && version == resetVersion && !VM.UseCookieLogOn && !VM.Succeed)
                    showError(ex.Message);
                Telemetry.LogException(ex);
            }
        }

        private void showError(string message)
        {
            VM.ErrorMsg = message;
            VM.ShowErrorMsg = true;
        }

        private void wv_NavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            navigationId = args.NavigationId;
            VM.ShowErrorMsg = false;
        }

        private void wv_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (isOpen && !isResetting && !VM.UseCookieLogOn && args.NavigationId == navigationId && !args.IsSuccess
                && args.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
                showError(args.WebErrorStatus.ToString());
        }

        private void wv_WebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            if (!isOpen || isResetting || VM.UseCookieLogOn || !isLogOnUri(args.Source)
                || args.Source != sender.CoreWebView2.Source)
                return;
            try
            {
                var data = JsonConvert.DeserializeObject<string[]>(args.WebMessageAsJson);
                if (data?.Length == 2)
                {
                    VM.UserName = data[0];
                    VM.Password = data[1];
                }
            }
            catch (JsonException) { }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var ww = Window.Current.Bounds.Width;
            if (ww > 400)
                wv.MinWidth = Math.Min(availableSize.Width - 144, 700);
            else
                wv.MinWidth = 0;

            return base.MeasureOverride(availableSize);
        }

        private async void cd_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;
            if (VM.UseCookieLogOn)
            {
                VM.LogOn.Execute();
            }
            else
            {
                await reset();
            }
        }

        private void cookie_TextChanged(object sender, RoutedEventArgs e)
        {
            if (((TextBox)sender).Text != "")
                VM.ShowErrorMsg = false;
        }

        private void cd_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Succeed)
                Hide();
        }

        private void cd_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            isOpen = false;
            resetVersion++;
            wv.Close();
        }

        private void cd_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            if (VM.LogOn.IsExecuting)
            {
                args.Cancel = true;
                return;
            }
            if (Succeed)
                return;
            if (VM.LogOnInfoBackup != null)
                Client.Current.RestoreLogOnInfo(VM.LogOnInfoBackup);
            if (Client.Current.NeedLogOn)
                Application.Current.Exit();
        }
    }
}
