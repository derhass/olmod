using System;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using UnityEngine;

namespace GameMod {
    
    public class TestClass {
        protected object c_renderer;

        public void Init() {
            c_renderer = new object();
        }
    }

    [HarmonyPatch(typeof(Debug), "Log")]
    class TestPatch {
        private static void Postfix() {
            TestClass test = new TestClass();
            test.Init();

            // this works
            Type renderLayerType = test.GetType();
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo fieldInfo = renderLayerType.GetField("c_renderer", bindingFlags);
            object r = (object)fieldInfo.GetValue(test);
            Debug.LogFormat("XXX {0}",r.ToString());


            // this crashes
            AccessTools.FieldRef<TestClass, object> theRendererRef = AccessTools.FieldRefAccess<TestClass, object>("c_renderer");
            Debug.LogFormat("YYY {0}",theRendererRef(test).ToString());
        }
    }
}
