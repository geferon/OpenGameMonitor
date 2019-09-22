using Microsoft.Extensions.Logging;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenGameMonitorWorker
{
    public class SteamAPIService : IDisposable
    {
        private readonly ILogger _logger;

        private SteamClient steamClient;
        private CallbackManager manager;

        private SteamUser steamUser;
        private SteamApps steamApps;

        public SteamAPIService(ILogger<SteamCMDService> logger)
        {
            _logger = logger;
            // app_info_print

            // create our steamclient instance
            steamClient = new SteamClient();
            // create the callback manager which will route callbacks to function calls
            manager = new CallbackManager(steamClient);

            // get the steamuser handler, which is used for logging on after successfully connecting
            steamUser = steamClient.GetHandler<SteamUser>();

            // get our steamapps handler, we'll use this as an example of how async jobs can be handled
            steamApps = steamClient.GetHandler<SteamApps>();

            // register a few callbacks we're interested in
            // these are registered upon creation to a callback manager, which will then route the callbacks
            // to the functions specified
            manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);

            manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);

            _logger.LogInformation("Connecting to Steam...");
            steamClient.Connect();

            manager.RunWaitAllCallbacks(TimeSpan.FromSeconds(15));
        }

        public void Dispose()
        {
            steamClient.Disconnect();
        }

        private void OnConnected(SteamClient.ConnectedCallback callback)
        {
            _logger.LogInformation("Connected to Steam! Logging in...");

            steamUser.LogOnAnonymous();
        }

        private void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            _logger.LogInformation("Disconnected from Steam");
        }

        private async void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                if (callback.Result == EResult.AccountLogonDenied)
                {
                    // if we recieve AccountLogonDenied or one of it's flavors (AccountLogonDeniedNoMailSent, etc)
                    // then the account we're logging into is SteamGuard protected
                    // see sample 5 for how SteamGuard can be handled

                    _logger.LogWarning("Unable to logon to Steam: This account is SteamGuard protected.");
                    return;
                }

                _logger.LogError("Unable to logon to Steam: {0} / {1}", callback.Result, callback.ExtendedResult);

                return;
            }

            // in this sample, we'll simply do a few async requests to acquire information about appid 440 (Team Fortress 2)

            // first, we'll request a depot decryption key for TF2's client/server shared depot (441)
            var depotJob = steamApps.GetDepotDecryptionKey(depotid: 441, appid: 440);

            // at this point, this request is now in-flight to the steam server, so we'll use te async/await pattern to wait for a response
            // the await pattern allows this code to resume once the Steam servers have replied to the request.
            // if Steam does not reply to the request in a timely fashion (controlled by the `Timeout` field on the AsyncJob object), the underlying
            // task for this job will be cancelled, and TaskCanceledException will be thrown.
            // additionally, if Steam encounters a remote failure and is unable to process your request, the job will be faulted and an AsyncJobFailedException
            // will be thrown.
            SteamApps.DepotKeyCallback depotKey = await depotJob;

            if (depotKey.Result == EResult.OK)
            {
                _logger.LogDebug($"Got our depot key: {BitConverter.ToString(depotKey.DepotKey)}");
            }
            else
            {
                _logger.LogDebug("Unable to request depot key!");
            }

            // now request some product info for TF2
            var productJob = steamApps.PICSGetProductInfo(440, package: null);

            // note that with some requests, Steam can return multiple results, so these jobs don't return the callback object directly, but rather
            // a result set that could contain multiple callback objects if Steam gives us multiple results
            AsyncJobMultiple<SteamApps.PICSProductInfoCallback>.ResultSet resultSet = await productJob;

            if (resultSet.Complete)
            {
                // the request fully completed, we can handle the data
                SteamApps.PICSProductInfoCallback productInfo = resultSet.Results.First();

                // ... do something with our product info
            }
            else if (resultSet.Failed)
            {
                // the request partially completed, and then Steam encountered a remote failure. for async jobs with only a single result (such as
                // GetDepotDecryptionKey), this would normally throw an AsyncJobFailedException. but since Steam had given us a partial set of callbacks
                // we get to decide what to do with the data

                // keep in mind that if Steam immediately fails to provide any data, or times out while waiting for the first result, an
                // AsyncJobFailedException or TaskCanceledException will be thrown

                // the result set might not have our data, so we need to test to see if we have results for our request
                SteamApps.PICSProductInfoCallback productInfo = resultSet.Results.FirstOrDefault(prodCallback => prodCallback.Apps.ContainsKey(440));

                if (productInfo != null)
                {
                    // we were lucky and Steam gave us the info we requested before failing
                }
                else
                {
                    // bad luck
                }
            }
            else
            {
                // the request partially completed, but then we timed out. essentially the same as the previous case, but Steam didn't explicitly fail.

                // we still need to check our result set to see if we have our data
                SteamApps.PICSProductInfoCallback productInfo = resultSet.Results.FirstOrDefault(prodCallback => prodCallback.Apps.ContainsKey(440));

                if (productInfo != null)
                {
                    // we were lucky and Steam gave us the info we requested before timing out
                }
                else
                {
                    // bad luck
                }
            }

            // lastly, if you're unable to use the async/await pattern (older VS/compiler, etc) you can still directly access the TPL Task associated
            // with the async job by calling `ToTask()`
            var depotTask = steamApps.GetDepotDecryptionKey(depotid: 441, appid: 440).ToTask();

            // set up a continuation for when this task completes
            var ignored = depotTask.ContinueWith(task =>
            {
                depotKey = task.Result;

                // do something with the depot key

                // we're finished with this sample, drop out of the callback loop

            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }


        private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            _logger.LogInformation("Logged off of Steam: {0}", callback.Result);
        }

        public async Task<SteamApps.PICSProductInfoCallback.PICSProductInfo> GetAppInfo(uint appID)
        {
            // now request some product info for TF2
            var productJob = steamApps.PICSGetProductInfo(appID, package: null, onlyPublic: false);

            // note that with some requests, Steam can return multiple results, so these jobs don't return the callback object directly, but rather
            // a result set that could contain multiple callback objects if Steam gives us multiple results
            AsyncJobMultiple<SteamApps.PICSProductInfoCallback>.ResultSet resultSet = await productJob;

            SteamApps.PICSProductInfoCallback productInfo;

            if (resultSet.Complete)
            {
                // the request fully completed, we can handle the data
                productInfo = resultSet.Results.First();
            }
            else
            {
                // the request partially completed, but then we timed out. essentially the same as the previous case, but Steam didn't explicitly fail.

                // we still need to check our result set to see if we have our data
                productInfo = resultSet.Results.FirstOrDefault(prodCallback => prodCallback.Apps.ContainsKey(appID));

                if (productInfo != null)
                {
                    // we were lucky and Steam gave us the info we requested before timing out
                }
                else
                {
                    throw new Exception("App Info couldn't be received");
                }
            }

            if (productInfo.Apps.Count == 0)
            {
                throw new Exception("No package found");
            }

            //return productInfo.Apps.FirstOrDefault(prod => prod.Key == appID).Value;
            return productInfo.Apps[appID];
        }
    }
}
