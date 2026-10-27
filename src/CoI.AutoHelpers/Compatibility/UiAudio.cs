using System;
using System.Reflection;

namespace CoI.AutoHelpers.Compatibility;

/// <summary>Uses the current UI sound player, with the prefab API retained for older games.</summary>
public static class UiAudio
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static Action Bind(object owner, string sound)
    {
        object? player = Read(owner, "UiSounds") ?? Read(owner, "m_uiSounds");
        Type? soundType = owner.GetType().Assembly.GetType("Mafi.Unity.Audio.UiSound");
        MethodInfo? play = soundType == null || player == null
            ? null : player.GetType().GetMethod("Play", new[] { soundType });
        if (play != null && Enum.IsDefined(soundType!, sound))
        {
            object value = Enum.Parse(soundType!, sound);
            return () => play.Invoke(player, new[] { value });
        }

        // PRE_4_3_COMPAT: retire this legacy audio fallback after Update 5 or when pre-4.3 support ends.
        object db = Read(owner, "AudioDb") ?? owner;
        object? source = db.GetType().GetMethod("GetSharedAudioUi", new[] { typeof(string) })
            ?.Invoke(db, new object[] { "Assets/Unity/UserInterface/Audio/" + sound + ".prefab" });
        source ??= Read(owner, sound + "Sound");
        MethodInfo? legacyPlay = source?.GetType().GetMethod("Play", Type.EmptyTypes);
        return () => legacyPlay?.Invoke(source, null);
    }

    private static object? Read(object owner, string name) =>
        owner.GetType().GetField(name, Members)?.GetValue(owner)
        ?? owner.GetType().GetProperty(name, Members)?.GetValue(owner);
}
