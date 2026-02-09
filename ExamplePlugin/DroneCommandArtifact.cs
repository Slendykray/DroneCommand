using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using RoR2;
using UnityEngine.AddressableAssets;
using RoR2.UI;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using RoR2.CharacterAI;

namespace DroneCommand
{
    class DroneCommandArtifact : ArtifactBase
    {
        public override string ArtifactName => "Artifact of Drone Command";
        public override string ArtifactLangTokenName => "ARTIFACT_OF_DRONE_COMMAND";
        public override string ArtifactDescription => "Choose your drones.";
        public override Sprite ArtifactEnabledIcon => Addressables.LoadAssetAsync<Sprite>("RoR2/Base/Command/texArtifactCommandEnabled.png").WaitForCompletion();
        public override Sprite ArtifactDisabledIcon => Addressables.LoadAssetAsync<Sprite>("RoR2/Base/Command/texArtifactCommandDisabled.png").WaitForCompletion();

        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateArtifact();
            Hooks();
        }
        private void CreateConfig(ConfigFile config)
        {

        }
        public override void Hooks()
        {
            On.RoR2.Interactor.PerformInteraction += HandleDrone;
            On.RoR2.CharacterMaster.Start += HandleEquipmentDrone;
        }


        private void HandleEquipmentDrone(On.RoR2.CharacterMaster.orig_Start orig, CharacterMaster self)
        {
            if (ArtifactEnabled)
            {
                //Log.Message("start random master " + self.bodyPrefab.name);

                if (self.bodyPrefab.GetComponent<CharacterBody>() == RoR2.RoR2Content.BodyPrefabs.EquipmentDroneBody)
                {
                    //Log.Warning("equipment drone!!!");
                    EquipmentIndex equipIndex = self.minionOwnership.ownerMaster.inventory.currentEquipmentIndex;
                    if (equipIndex != EquipmentIndex.None)
                    {
                        self.inventory.SetEquipmentIndex(equipIndex, false);
                    }

                }
            }      

            orig(self);
        }

        private void HandleDrone(On.RoR2.Interactor.orig_PerformInteraction orig, Interactor self, GameObject interactableObject)
        {
            if (NetworkServer.active && ArtifactEnabled)
            {
                if ((interactableObject.GetComponent<SummonMasterBehavior>() || interactableObject.GetComponent<DroneVendorTerminalBehavior>()) &&
                    interactableObject.GetComponent<PurchaseInteraction>().CanBeAffordedByInteractor(self))
                {

                    GameObject cube = RoR2.Artifacts.CommandArtifactManager.commandCubePrefab;

                    HUD hud = HUD.readOnlyInstanceList[0];
                    GameObject pickerObj = UnityEngine.Object.Instantiate(
                         cube.GetComponent<PickupPickerController>().panelPrefab, hud.transform);


                    PickupPickerPanel picker = pickerObj.GetComponent<PickupPickerPanel>();
                    PickupPickerPanel pickerPref = cube.GetComponent<PickupPickerController>().panelPrefab.GetComponent<PickupPickerPanel>();


                    PickupPickerController controller = UnityEngine.Object.Instantiate<PickupPickerController>(
                        cube.GetComponent<PickupPickerController>(), pickerObj.transform);

                    controller.panelInstance = pickerObj;
                    controller.panelInstanceController = picker;

                    picker.pickerController = controller;
            


                    DroneDef[] drones = DroneCatalog.droneDefs;

                    List<PickupPickerController.Option> optionList = new List<PickupPickerController.Option>();

                    for (int i = 0; i < drones.Length; i++)
                    {
                        PickupIndex index = PickupCatalog.FindPickupIndex(drones[i].droneIndex);

                        optionList.Add(new PickupPickerController.Option
                        {
                            pickup = new UniquePickup(index),
                            available = true,
                        });
                    }
                  
                    controller.SetOptionsServer(optionList.ToArray());


                    //override color
                    Image[] array = picker.coloredImages;
                    Image[] array2 = pickerPref.coloredImages;
                    for (int i = 0; i < array.Length; i++)
                    {
                        array[i].color = array2[i].color;
                        array[i].color *= new Color32(41, 101, 255, 255);
                    }
                    array = picker.darkColoredImages;
                    array2 = pickerPref.darkColoredImages;
                    for (int i = 0; i < picker.darkColoredImages.Length; i++)
                    {
                        array[i].color = array2[i].color;
                        array[i].color *= new Color32(41, 101, 255, 255);
                    }


                    //remove junk event
                    controller.onUniquePickupSelected = new PickupPickerController.UniquePickupUnityEvent();

                    controller.onUniquePickupSelected.AddListener((pickup) =>
                    {
                        if (!NetworkServer.active)
                            return;

                        DroneIndex droneIndex = PickupCatalog.GetPickupDef(pickup.pickupIndex).droneIndex;
                        DroneDef drone = DroneCatalog.GetDroneDef(droneIndex);
                      
                        SummonMasterBehavior s = interactableObject.GetComponent<SummonMasterBehavior>();
                        DroneVendorTerminalBehavior t = interactableObject.GetComponent<DroneVendorTerminalBehavior>();
                        if (s)
                        {
                            s.masterPrefab = drone.masterPrefab;
                        }
                        if (t)
                        {
                            t.currentPickup = new UniquePickup(pickup.pickupIndex);
                        }


                        orig(self, interactableObject);                   

                        UnityEngine.Object.Destroy(pickerObj);
                    });


                    return;
                }
            }

            orig(self, interactableObject);

        }
    }
}