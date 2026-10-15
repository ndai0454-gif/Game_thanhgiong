using System;
using System.IO;
using UnityEngine;

// Integration checks isolate progress and mixer preferences, including when Play is stopped early.
public static class LegendVerificationPreferences
{
    [Serializable]public sealed class Entry{public string key,text;public bool exists;public float number;}
    [Serializable]public sealed class Backup{public bool active;public Entry[] checkpoints,audio;}
    static string FilePath=>Path.GetFullPath(Path.Combine(Application.dataPath,"../IsometricVerification/preferences_backup.json"));
    public static void Capture()
    {
        Restore();Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        var maps=GodotPortRuntimeVerifier.Maps;
        var data=new Backup{active=true,checkpoints=new Entry[maps.Length],audio=new Entry[3]};
        for(int i=0;i<maps.Length;i++){string key=LegendCheckpoint.StorageKeyForScene(maps[i]);data.checkpoints[i]=new Entry{key=key,exists=PlayerPrefs.HasKey(key),text=PlayerPrefs.GetString(key)};}
        string[] keys={"TG.Audio.Master","TG.Audio.Music","TG.Audio.Effects"};for(int i=0;i<3;i++)data.audio[i]=new Entry{key=keys[i],exists=PlayerPrefs.HasKey(keys[i]),number=PlayerPrefs.GetFloat(keys[i],1)};
        File.WriteAllText(FilePath,JsonUtility.ToJson(data,true));
        // Persist the complete backup before the first mutation. Never assume a fixed scene count.
        foreach(string scene in maps)LegendCheckpoint.ForgetScene(scene);
    }
    public static void Restore()
    {
        if(!File.Exists(FilePath))return;var data=JsonUtility.FromJson<Backup>(File.ReadAllText(FilePath));if(data==null||!data.active)return;
        LegendAudioMix.SetMix(data.audio[0].number,data.audio[1].number,data.audio[2].number);
        foreach(var item in data.checkpoints)if(item.exists)PlayerPrefs.SetString(item.key,item.text);else PlayerPrefs.DeleteKey(item.key);
        foreach(var item in data.audio)if(item.exists)PlayerPrefs.SetFloat(item.key,item.number);else PlayerPrefs.DeleteKey(item.key);
        PlayerPrefs.Save();data.active=false;File.WriteAllText(FilePath,JsonUtility.ToJson(data,true));Debug.Log("ISOMETRIC_VERIFY restored original checkpoint and audio preferences");
    }
}
