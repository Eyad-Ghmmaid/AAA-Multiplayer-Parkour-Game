using System.Collections.Generic;
using UnityEngine;

namespace FS_Core
{
    public class FSPackageData
    {
        public static List<FSPackageInfo> packages = new List<FSPackageInfo>()
        {
            new FSPackageInfo()
            {
                packageName = "Parkour And Climbing System",
                iconPath = "Icons/Parkour_icon",
                youtubeLink = "https://www.youtube.com/playlist?list=PLnbdyws4rcAu8b3zKIyTMKhMK8iHsIaOn",
                documentationLink = "https://fantacode.gitbook.io/fantacode-studios/fantacode-studios/fs-parkour-and-climbing-system",
                assetLink = "https://af.unity.com/ot/AJp8vyR/"

                //addOns = new List<FSPackageInfo>()
                //{
                //    new FSPackageInfo()
                //    {
                //        packageName = "Parkour And Climbing AI",
                //        iconPath = "Icons/ParkourAI_icon",
                //        youtubeLink = "https://www.youtube.com/playlist?list=PLnbdyws4rcAsydRZvhwi-SO41x8ysESMe",
                //        documentationLink = "https://fantacode.gitbook.io/parkour-and-climbing-ai",
                //        assetLink = "https://assetstore.unity.com/packages/tools/game-toolkits/fs-parkour-and-climbing-ai-269625"
                //    }
                //}

            },

            new FSPackageInfo()
            {
                packageName = "Parkour And Climbing AI",
                iconPath = "Icons/ParkourAI_icon",
                youtubeLink = "https://www.youtube.com/playlist?list=PLnbdyws4rcAsydRZvhwi-SO41x8ysESMe",
                documentationLink = "https://fantacode.gitbook.io/fantacode-studios/fantacode-studios/fs-parkour-and-climbing-ai",
                assetLink = "https://af.unity.com/ot/dapxOP5/"
            },

            new FSPackageInfo()
            {
                packageName = "Melee Combat System",
                iconPath = "Icons/Melee_icon",
                youtubeLink = "https://www.youtube.com/playlist?list=PLnbdyws4rcAuJ6g8xrL9r2K3vv2SwL1na",
                documentationLink = "https://fantacode.gitbook.io/fantacode-studios/fantacode-studios/fs-melee-combat-system",
                assetLink = "https://af.unity.com/ot/gx2L3zQ/"
            },

            new FSPackageInfo()
            {
                packageName = "Grappling Hook System",
                iconPath = "Icons/Grappling_icon",
                youtubeLink = "https://www.youtube.com/playlist?list=PLnbdyws4rcAsY-QDOKJdTjBoTNZknwcWL",
                documentationLink = "https://fantacode.gitbook.io/fantacode-studios/fantacode-studios/fs-grappling-hook-system",
                assetLink = "https://af.unity.com/ot/4mp4yjp/"
            },

            new FSPackageInfo()
            {
                packageName = "Rope Swinging System",
                iconPath = "Icons/Rope_icon",
                youtubeLink = "https://youtube.com/playlist?list=PLnbdyws4rcAuQEdnCE8hGWdXDSDdcYS09&si=GlmMI4PNtTNyhSTM",
                documentationLink = "https://fantacode.gitbook.io/fantacode-studios/fantacode-studios/fs-rope-swinging-system",
                assetLink = "https://af.unity.com/ot/o3xGnlA/"
            },

            new FSPackageInfo()
            {
                packageName = "Dialogue And Cutscene System",
                iconPath = "Icons/Cutscene_icon",
                youtubeLink = "https://www.youtube.com/playlist?list=PLnbdyws4rcAt_3RFIDzCJZpfyN51uMRRk",
                documentationLink = "https://fantacode.gitbook.io/cutscene-system",
                assetLink = "https://prf.hn/l/7JAzgzW/"
            }
            ,

            new FSPackageInfo()
            {
                packageName = "Inventory System",
                iconPath = "Icons/Inventory_icon",
                youtubeLink = "https://youtube.com/playlist?list=PLnbdyws4rcAsP4g_2BOR5NGtPwJ6S7UZI&si=lOTvTZL6DB6qnPux",
                documentationLink = "https://fantacode.gitbook.io/fantacode-studios/fantacode-studios/fs-inventory-system",
                assetLink = "https://af.unity.com/ot/318zMnE/"
            }
            ,
            new FSPackageInfo()
            {
                packageName = "Shooter System",
                iconPath = "Icons/Shooter_icon",
                youtubeLink = "https://www.youtube.com/playlist?list=PLnbdyws4rcAtm31IUrtxHMyMwfSCS1Heb",
                documentationLink = "https://fantacode.gitbook.io/fantacode-studios/fantacode-studios/fs-shooter-system",
                assetLink = "https://af.unity.com/ot/lJe9Wll/"
            },
            new FSPackageInfo()
            {
                packageName = "Swimming System",
                iconPath = "Icons/Swim_icon",
                youtubeLink = "https://www.youtube.com/playlist?list=PLnbdyws4rcAusHkS0Zn8Mq6jUHpb0Sy8b",
                documentationLink = "https://fantacode.gitbook.io/fantacode-studios/fantacode-studios/fs-swimming-system",
                assetLink = "https://af.unity.com/ot/55DwZYo/"
            },
            new FSPackageInfo()
            {
                packageName = "Stealth System",
                iconPath = "Icons/Stealth_icon",
                youtubeLink = "https://www.youtube.com/playlist?list=PLRuXbyjnt_3A",
                documentationLink = "https://fantacode.gitbook.io/fantacode-studios/fantacode-studios/fs-stealth-system",
                assetLink = "https://assetstore.unity.com/packages/slug/407220"
            }
        };

        public static void UnSelectAll(List<FSPackageInfo> info = null)
        {
            if (info == null)
                info = packages;
            foreach (var p in info)
            {
                p.opened = false;
                if (p.addOns.Count > 0)
                    UnSelectAll(p.addOns);
            }
        }
    }

    public class FSPackageInfo
    {
        public string packageName;
        public string youtubeLink;
        public string documentationLink;
        public string assetLink;
        public string iconPath;

        public Texture2D icon;


        public List<FSPackageInfo> addOns = new List<FSPackageInfo>();

        public bool opened;
    }

}