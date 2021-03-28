using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using Overload;
using UnityEngine;

namespace GameMod {

    [HarmonyPatch(typeof(UIManager), "GenerateUICollisionMesh")]
    class UIMeshColliderNoRender_GenerateUICollisionMesh {
        private static void Postfix() {
            Overload.UIManager.url[4].c_mesh.Clear();
        }
    }
}
