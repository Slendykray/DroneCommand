using UnityEngine;
using System.IO;
using System.Reflection;
using UnityEngine.AddressableAssets;
using RoR2;

namespace DroneCommand
{
    public static class Prefabs
    {
        public static GameObject pickerPrefab;

        public static void Init()
        {
            pickerPrefab = R2API.PrefabAPI.InstantiateClone(Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Command/CommandCube.prefab").WaitForCompletion(), "gofuckyourself");

            Rigidbody rb = pickerPrefab.GetComponent<Rigidbody>();
            rb.isKinematic = true;

            foreach (Transform child in pickerPrefab.transform)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }


            PickupPickerController controller = pickerPrefab.GetComponent<PickupPickerController>();

            //remove all events
            controller.onUniquePickupSelected = new PickupPickerController.UniquePickupUnityEvent();


            GameObject newPanel = R2API.PrefabAPI.InstantiateClone(controller.panelPrefab, "fuckingpanel");
            controller.panelPrefab = newPanel;
        }
    }
}
