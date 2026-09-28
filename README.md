# Pawjections

An interactive game system designed to encourage cats to engage in active play.

Pawjections finds your cat with a webcam, using the
[YOLOX](https://github.com/Megvii-BaseDetection/YOLOX) object detector running
in Unity's
[Inference Engine](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/index.html).

## Contributing

Hello, thank you so much for wanting to contribute to Pawjections!

### Prerequisites

- [Unity 6000.6.3f1](https://unity.com/releases/editor/archive)
(install through Unity Hub)
- [Git LFS](https://git-lfs.com/). Binary assets
(images, audio, video, fonts, models) are stored with LFS.
Run `git lfs install` once before cloning.

### Getting started

1. [Fork the repository](https://github.com/pawjections/pawjections-unity-client/fork)
2. Clone your fork and open the project folder in Unity Hub.
3. Create a branch for your change
(ideally one Issue per branch for easy code review).
4. Commit your changes and push your branch to your fork.
5. Create a Pull Request targeting `main`.

### Merging scenes and prefabs (optional)

Unity's [Smart Merge](https://docs.unity3d.com/6000.6/Documentation/Manual/SmartMerge.html)
tool, UnityYAMLMerge, resolves conflicts in scenes, prefabs, and other Unity
YAML files much better than a plain text merge. To use it, add the following
to your `.git/config` or `~/.gitconfig`:

```ini
[merge]
    tool = unityyamlmerge
[merge "unityyamlmerge"]
    name = Unity SmartMerge
    driver = '<path to UnityYAMLMerge>' merge -h -p --force --fallback none %O %B %A %A
    recursive = binary
[mergetool "unityyamlmerge"]
    trustExitCode = false
    cmd = '<path to UnityYAMLMerge>' merge -p \"$BASE\" \"$REMOTE\" \"$LOCAL\" \"$MERGED\"
```

The `merge "unityyamlmerge"` driver runs automatically on every merge and
rebase for the files `.gitattributes` marks as `unity-yaml`. Without it, Git
silently falls back to a plain text merge for them.

When the driver hits a real conflict (both sides changed the same property),
the file is marked as conflicted but contains no conflict markers, only the
incoming value. Resolve it with `git mergetool` rather than `git add`.

For editors installed through Unity Hub, `<path to UnityYAMLMerge>` is:

- macOS: `/Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/Helpers/UnityYAMLMerge`
- Windows: `C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Data\Tools\UnityYAMLMerge.exe`
