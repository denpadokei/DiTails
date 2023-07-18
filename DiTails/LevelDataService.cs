using BeatSaverSharp;
using BeatSaverSharp.Models;
using IPA.Loader;
using SiraUtil.Logging;
using SiraUtil.Zenject;
using System;
using System.Threading;
using System.Threading.Tasks;
using Zenject;

namespace DiTails
{
    internal class LevelDataService : ILateDisposable
    {
        private readonly SiraLog _siraLog;
        private readonly BeatSaver _beatSaver;
        private readonly IPlatformUserModel _platformUserModel;

        internal LevelDataService(SiraLog siraLog, IPlatformUserModel platformUserModel, UBinder<Plugin, PluginMetadata> metadataBinder)
        {
            this._siraLog = siraLog;
            this._platformUserModel = platformUserModel;
            this._beatSaver = new BeatSaver("DiTails", Version.Parse(metadataBinder.Value.HVersion.ToString()));
        }

        public void LateDispose()
        {
            this._beatSaver.Clear();
            this._beatSaver.Dispose();
        }

        internal async Task<Beatmap?> GetBeatmap(IDifficultyBeatmap difficultyBeatmap, CancellationToken token)
        {
            if (!difficultyBeatmap.level.levelID.Contains("custom_level_")) {
                return null;
            }
            var hash = difficultyBeatmap.level.levelID.Replace("custom_level_", "");
            var beatmap = await this._beatSaver.BeatmapByHash(hash, token);
            return beatmap ?? null;
        }

        internal async Task<Beatmap> Vote(Beatmap beatmap, bool upvote, CancellationToken token)
        {
            try {
                var steam = false;
                var steamPlatformUserModelType = this.GetSteamUserModelType();
                var oculusPlatformUserModel = this.GetOculusUserModelType();
                if (steamPlatformUserModelType != null && this._platformUserModel.GetType() == steamPlatformUserModelType) {
                    steam = true;
                }
                else if (oculusPlatformUserModel != null && this._platformUserModel.GetType() == oculusPlatformUserModel) {
                    steam = false;
                }
                else {
                    this._siraLog.Debug("Current platform cannot vote.");
                    return beatmap;
                }
                var info = await this._platformUserModel.GetUserInfo();
                var authToken = await this._platformUserModel.GetUserAuthToken();
                var ticket = authToken.token;

                this._siraLog.Debug("Starting Vote...");
                ticket = steam ? ticket.Replace("-", "") : authToken.token;

                var response = await beatmap.LatestVersion.Vote(upvote ? BeatSaverSharp.Models.Vote.Type.Upvote : BeatSaverSharp.Models.Vote.Type.Downvote,
                    steam ? BeatSaverSharp.Models.Vote.Platform.Steam : BeatSaverSharp.Models.Vote.Platform.Oculus,
                    info.platformUserId,
                    ticket, token);

                this._siraLog.Info(response.Successful);
                this._siraLog.Info(response.Error ?? "good");
                if (response.Successful) {
                    await beatmap.Refresh();
                }
                this._siraLog.Debug($"Voted. Upvote? ({upvote})");
            }
            catch (Exception e) {
                this._siraLog.Error(e.Message);
            }
            return beatmap;
        }

        /// <summary>
        /// Oculusでは存在しないクラスのため、直接参照するとビルドできない
        /// </summary>
        /// <returns></returns>
        private Type GetSteamUserModelType()
        {
            return Type.GetType("SteamPlatformUserModel, Main");
        }

        private Type GetOculusUserModelType()
        {
            return Type.GetType("OculusPlatformUserModel, Main");
        }
    }
}