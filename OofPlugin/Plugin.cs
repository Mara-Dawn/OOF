using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using NAudio.Wave;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;


//shoutout anna clemens

namespace OofPluginFixed
{
    public class Service {
        [PluginService] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] public static IFramework Framework { get; private set; } = null!;
        [PluginService] public static IClientState ClientState { get; private set; } = null!;
        [PluginService] public static ICondition Condition { get; private set; } = null!;
        [PluginService] public static IPartyList PartyList { get; private set; } = null!;
        [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] public static Configuration Configuration { get; set; } = null!;
        [PluginService] public static PluginUI PluginUi { get; set; } = null!;
        [PluginService] public static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    }

    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "OOF";

        private const string oofCommand = "/oof";
        private const string oofSettings = "/oofsettings";
        private const string oofVideo = "/oofvideo";

        private OofHelpers OofHelpers { get; init; }

        // i love global variables!!!! the more global the more globaly it gets
        // sound
        public bool isSoundPlaying { get; set; } = false;
        // private WaveStream? reader;
        private DirectSoundOut? soundOut;
        private string? soundFile { get; set; }

        //check for fall
        private float prevPos { get; set; } = 0;
        private float prevVel { get; set; } = 0;
        private float distJump { get; set; } = 0;
        private bool wasJumping { get; set; } = false;

        //public class DeadPlayer
        //{
        //    public uint PlayerId;
        //    public bool DidPlayOof = false;
        //    public float Distance = 0;
        //}
        //public List<DeadPlayer> DeadPlayers { get; set; } = new List<DeadPlayer>();

        public CancellationTokenSource CancelToken;

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface PluginInterface)
        {
            PluginInterface.Create<Service>();

            Service.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Service.Configuration.Initialize(PluginInterface);

            Service.PluginUi = new PluginUI(Service.Configuration, this, PluginInterface);
            OofHelpers = new OofHelpers();

            // load audio file. idk if this the best way
            LoadSoundFile();

            Service.CommandManager.AddHandler(oofCommand, new CommandInfo(OnCommand)
            {
                HelpMessage = "play oof sound"
            });
            Service.CommandManager.AddHandler(oofSettings, new CommandInfo(OnCommand)
            {
                HelpMessage = "change oof settings"
            });
            Service.CommandManager.AddHandler(oofVideo, new CommandInfo(OnCommand)
            {
                HelpMessage = "open Hbomberguy video on OOF.mp3"
            });

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            Service.Framework.Update += FrameworkOnUpdate;

            // lmao
            CancelToken = new CancellationTokenSource();
            Task.Run(() => OofAudioPolling(CancelToken.Token));

        }
        public void LoadSoundFile()
        {
            if (Service.Configuration.DefaultSoundImportPath.Length == 0)
            {
                var path = Path.Combine(Service.PluginInterface.AssemblyLocation.Directory?.FullName!, "oof.wav");
                soundFile = path;
                return;
            }
            soundFile = Service.Configuration.DefaultSoundImportPath;
        }
        private void OnCommand(string command, string args)
        {
            if (command == oofCommand) PlaySound(CancelToken.Token);
            if (command == oofSettings) Service.PluginUi.SettingsVisible = true;
            if (command == oofVideo) OpenVideo();

        }

        private void DrawUI()
        {
            Service.PluginUi.Draw();
        }

        private void DrawConfigUI()
        {
            Service.PluginUi.SettingsVisible = true;
        }
        private void FrameworkOnUpdate(IFramework framework)
        {
            if (Service.ClientState == null || Service.ClientState.LocalPlayer == null) return;
            try
            {
                if (Service.Configuration.OofOnFall) CheckFallen();
                if (Service.Configuration.OofOnDeath) CheckDeath();
            }
            catch (Exception e)
            {
                PluginLog.Error("failed to check for oof condition:", e.Message);
            }
        }

        /// <summary>
        /// check if player has died during alliance, party, and self.
        /// this may be the worst if statement chain i have made
        /// </summary>
        private void CheckDeath()
        {
            if (!Service.Configuration.OofOnDeathBattle && Service.Condition[ConditionFlag.InCombat]) return;

            if (Service.PartyList != null && Service.PartyList.Any())
            {
                if (Service.Configuration.OofOnDeathAlliance && Service.PartyList.Length == 8 && Service.PartyList.GetAllianceMemberAddress(0) != IntPtr.Zero) // the worst "is alliance" check
                {
                    try
                    {
                        for (int i = 0; i < 16; i++)
                        {
                            var allianceMemberAddress = Service.PartyList.GetAllianceMemberAddress(i);
                            if (allianceMemberAddress == IntPtr.Zero) throw new NullReferenceException("allience member address is null");

                            var allianceMember = Service.PartyList.CreateAllianceMemberReference(allianceMemberAddress) ?? throw new NullReferenceException("allience reference is null");
                            OofHelpers.AddRemoveDeadPlayer(allianceMember);
                        }
                    }
                    catch (Exception e)
                    {
                        PluginLog.LogError("failed alliance check", e.Message);
                    }
                }
                if (Service.Configuration.OofOnDeathParty)
                {
                    foreach (var member in Service.PartyList)
                    {
                        OofHelpers.AddRemoveDeadPlayer(member, member.Territory.Id == Service.ClientState!.TerritoryType);
                    }
                }

            }
            else
            {
                if (Service.Configuration.OofOnDeathSelf) return;
                OofHelpers.AddRemoveDeadPlayer(Service.ClientState!.LocalPlayer!);
            }

        }

        /// <summary>
        /// check if player has taken fall damage (brute force way)
        /// </summary>
        private void CheckFallen()
        {
            // dont run btwn moving areas & also wont work in combat
            if (Service.Condition[ConditionFlag.BetweenAreas]) return;
            if (!Service.Configuration.OofOnFallBattle && Service.Condition[ConditionFlag.InCombat]) return;
            if (!Service.Configuration.OofOnFallMounted && (Service.Condition[ConditionFlag.Mounted] || Service.Condition[ConditionFlag.Mounted2])) return;

            var isJumping = Service.Condition[ConditionFlag.Jumping];
            var pos = Service.ClientState!.LocalPlayer!.Position.Y;
            var velocity = prevPos - pos;

            if (isJumping && !wasJumping)
            {
                if (prevVel < 0.17) distJump = pos; //started falling
            }
            else if (wasJumping && !isJumping)  // stopped falling
            {
                if (distJump - pos > 9.60) PlaySound(CancelToken.Token); // fell enough to take damage // i guessed and checked this distance value btw
            }

            // set position for next timestep
            prevPos = pos;
            prevVel = velocity;
            wasJumping = isJumping;
        }
        public void StopSound()
        {
            soundOut?.Pause();
            soundOut?.Dispose();

        }
        /// <summary>
        /// Play sound but without referencing windows.forms.
        /// much of the code from: https://github.com/kalilistic/Tippy/blob/5c18d6b21461b0bbe4583a86787ef4a3565e5ce6/src/Tippy/Tippy/Logic/TippyController.cs#L11
        /// </summary>
        /// <param name="token">cancellation token</param>
        /// <param name="volume">optional volume param</param>
        public void PlaySound(CancellationToken token, float volume = 1)
        {
            Task.Run(() =>
            {
                isSoundPlaying = true;
                WaveStream reader;
                try
                {
                    reader = new MediaFoundationReader(soundFile);
                }
                catch (Exception ex)
                {
                    isSoundPlaying = false;
                    PluginLog.Error("Failed read file", ex);
                    return;
                }

                var audioStream = new WaveChannel32(reader)
                {
                    Volume = Service.Configuration.Volume * volume,
                    PadWithZeroes = false // you need this or else playbackstopped event will not fire
                };
                using (reader)
                {
                    if (isSoundPlaying && soundOut != null)
                    {
                        soundOut.Pause();
                        soundOut.Dispose();
                    };
                    //shoutout anna clemens for the winforms fix
                    soundOut = new DirectSoundOut();

                    try
                    {
                        soundOut.Init(audioStream);
                        soundOut.Play();
                        soundOut.PlaybackStopped += OnPlaybackStopped;
                        // run after sound has played. does this work? i have no idea
                        void OnPlaybackStopped(object? sender, StoppedEventArgs e)
                        {
                            soundOut.PlaybackStopped -= OnPlaybackStopped;
                            isSoundPlaying = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        isSoundPlaying = false;
                        PluginLog.Error("Failed play sound", ex);
                        return;
                    }
                }

            }, token);
        }

        /// <summary>
        /// open the hbomberguy video on oof
        /// </summary>
        public static void OpenVideo()
        {
            Util.OpenLink("https://www.youtube.com/watch?v=0twDETh6QaI");
        }

        /// <summary>
        /// check deadPlayers every once in a while. prevents multiple oof from playing too fast
        /// </summary>
        /// <param name="token"> cancellation token</param>
        private async Task OofAudioPolling(CancellationToken token)
        {
            while (true)
            {
                await Task.Delay(200, token);
                if (token.IsCancellationRequested) break;
                if (!OofHelpers.DeadPlayers.Any()) continue;
                if (Service.ClientState!.LocalPlayer! == null) continue;
                foreach (var player in OofHelpers.DeadPlayers)
                {
                    if (player.DidPlayOof) continue;
                    float volume = 1f;
                    if (Service.Configuration.DistanceBasedOof && player.Distance != Vector3.Zero && player.Distance != Service.ClientState!.LocalPlayer!.Position)
                    {
                        var dist = Vector3.Distance(Service.ClientState!.LocalPlayer!.Position, player.Distance);
                        volume = Math.Max(Service.Configuration.DistanceMinVolume, 1f / (dist * Service.Configuration.DistanceFalloff));
                    }
                    PlaySound(token, volume);
                    player.DidPlayOof = true;
                    break;

                }
            }
        }

        /// <summary>
        /// dispose
        /// </summary>
        public void Dispose()
        {
            Service.PluginUi.Dispose();
            Service.CommandManager.RemoveHandler(oofCommand);
            Service.CommandManager.RemoveHandler(oofSettings);
            Service.CommandManager.RemoveHandler(oofVideo);
            CancelToken.Cancel();
            CancelToken.Dispose();

            Service.Framework.Update -= FrameworkOnUpdate;
            try
            {
                while (isSoundPlaying)
                {
                    Thread.Sleep(100);
                    soundOut?.Pause();
                    isSoundPlaying = false;

                }
                soundOut?.Dispose();
            }
            catch (Exception e)
            {
                PluginLog.LogError("Failed to dispose oofplugin controller", e.Message);
            }


        }


    }
}
