using System;
using ProjectSurvive.Core.Application.Settings;
using ProjectSurvive.Core.Contracts.Save;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Infrastructure.Save
{
    /// <summary>
    /// Maps <see cref="GameSettings"/> to the MessagePack settings DTO and back.
    /// </summary>
    public sealed class MessagePackSettingsCodec : ISettingsCodec
    {
        /// <summary>True when the generated (AOT-safe) formatter exists in this build.</summary>
        public static bool GeneratedFormatterIsPresent => SettingsCodec.GeneratedFormatterIsPresent;

        /// <inheritdoc />
        public byte[] Encode(GameSettings settings)
        {
            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return SettingsCodec.Serialize(new SettingsDto
            {
                SchemaVersion = SettingsService.SchemaVersion,
                MasterVolume = settings.MasterVolume,
                Language = (int)settings.Language,
                DefaultSpeed = settings.DefaultBattleSpeed,
            });
        }

        /// <inheritdoc />
        public GameSettings Decode(byte[] bytes)
        {
            if (bytes is null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            SettingsDto dto = SettingsCodec.Deserialize(bytes);
            if (dto.SchemaVersion != SettingsService.SchemaVersion)
            {
                throw new InvalidOperationException("Unsupported settings schema: " + dto.SchemaVersion);
            }

            if (float.IsNaN(dto.MasterVolume) || dto.MasterVolume < GameSettings.MinVolume
                || dto.MasterVolume > GameSettings.MaxVolume)
            {
                throw new InvalidOperationException("Settings carry an invalid volume.");
            }

            if (dto.DefaultSpeed != 1f && dto.DefaultSpeed != 2f)
            {
                throw new InvalidOperationException("Settings carry an invalid battle speed.");
            }

            var language = dto.Language switch
            {
                1 => Language.Th,
                2 => Language.En,
                _ => throw new InvalidOperationException("Settings carry an unknown language: " + dto.Language),
            };

            var settings = new GameSettings();
            settings.SetVolume(dto.MasterVolume);
            settings.SetLanguage(language);
            settings.SetDefaultBattleSpeed(dto.DefaultSpeed);
            return settings;
        }
    }
}
