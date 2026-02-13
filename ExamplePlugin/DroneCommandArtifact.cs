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
using R2API;
using IL.RoR2.ContentManagement;
using RiskOfOptions;
using RiskOfOptions.Options;
using RiskOfOptions.OptionConfigs;

namespace DroneCommand
{
    class DroneCommandArtifact : ArtifactBase
    {
        public override string ArtifactName => "Artifact of Drone Command";
        public override string ArtifactLangTokenName => "ARTIFACT_OF_DRONE_COMMAND";
        public override string ArtifactDescription => "Choose your drones.";
        public override Sprite ArtifactEnabledIcon => Asset.mainBundle.LoadAsset<Sprite>("DroneCommandEnabled.png");
        public override Sprite ArtifactDisabledIcon => Asset.mainBundle.LoadAsset<Sprite>("DroneCommandDisabled.png");

        public static ConfigEntry<bool> sameTier;
        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateArtifact();
            Hooks();
            CreateFakeItemDefs();
        }
        private void CreateConfig(ConfigFile config)
        {
            sameTier = config.Bind<bool>("Artifact: " + ArtifactName, "SameTier", true, "Only allow to pick drones from the same tier (boring)");

            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions"))
            {
                Sprite icon = Asset.mainBundle.LoadAsset<Sprite>("DroneCommandEnabled.png");
                ModSettingsManager.SetModIcon(icon);

                ModSettingsManager.AddOption(new CheckBoxOption(sameTier));          
            }
        }
        public override void Hooks()
        {
            On.RoR2.Interactor.PerformInteraction += HandleDrone;
            On.RoR2.CharacterMaster.Start += HandleEquipmentDrone;
            On.RoR2.ItemCatalog.Init += ResolveFakeItemDefs;
            On.RoR2.UI.PickupPickerPanel.SetPickupOptions += OverrideColor;
            On.RoR2.PickupPickerController.OnDisplayEnd += Cleanup;
        }

        private void Cleanup(On.RoR2.PickupPickerController.orig_OnDisplayEnd orig, PickupPickerController self, NetworkUIPromptController networkUIPromptController, LocalUser localUser, CameraRigController cameraRigController)
        {
            orig(self, networkUIPromptController, localUser, cameraRigController);

            if (self.transform.name.Contains("gofuckyourself"))
            {
                UnityEngine.Object.Destroy(self.gameObject);
            }
        }

        private void OverrideColor(On.RoR2.UI.PickupPickerPanel.orig_SetPickupOptions orig, PickupPickerPanel self, PickupPickerController.Option[] options)
        {
            orig(self, options);

            if (self.transform.name.Contains("fuckingpanel"))
            {
                PickupPickerPanel origPicker = RoR2.Artifacts.CommandArtifactManager.commandCubePrefab.GetComponent<PickupPickerController>().panelPrefab.GetComponent<PickupPickerPanel>();
                PickupPickerPanel picker = self;
                Image[] array2 = origPicker.coloredImages;
                Image[] array = picker.coloredImages;
                for (int i = 0; i < array.Length; i++)
                {
                    array[i].color = array2[i].color;
                    array[i].color *= new Color32(41, 101, 255, 255);
                }
                array2 = origPicker.darkColoredImages;
                array = picker.darkColoredImages;
                for (int i = 0; i < picker.darkColoredImages.Length; i++)
                {
                    array[i].color = array2[i].color;
                    array[i].color *= new Color32(41, 101, 255, 255);
                }
            }
        }

        private Dictionary<DroneIndex, ItemIndex> droneToFakeItem = new();
        private Dictionary<ItemIndex, DroneIndex> fakeItemToDrone = new();

        private int fakeItemsPool = 50;
        private List<string> fakeItemNames = new();

        private void ResolveFakeItemDefs(On.RoR2.ItemCatalog.orig_Init orig)
        {
            orig();

            DroneDef[] drones = DroneCatalog.droneDefs;

            for (int i = 0; i < drones.Length; i++)
            {
                ItemIndex itemIndex = ItemCatalog.FindItemIndex(fakeItemNames[i]);
                DroneIndex droneIndex = drones[i].droneIndex;

                DroneDef drone = drones[i];
                ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);

                itemDef.name = drone.name;
                itemDef.nameToken = drone.nameToken;
                itemDef.pickupToken = drone.pickupToken;
                itemDef.descriptionToken = drone.descriptionToken;   

                itemDef.pickupIconSprite = drone.iconSprite;

                droneToFakeItem.Add(droneIndex, itemIndex);
                fakeItemToDrone.Add(itemIndex, droneIndex);
            }
        }    
        private void CreateFakeItemDefs()
        {
            for (int i = 0; i < fakeItemsPool; i++)
            {
                ItemDef itemDef = ScriptableObject.CreateInstance<ItemDef>();

                itemDef.name = $"FAKE_DRONE_{i}_NAME";
                itemDef.nameToken = $"FAKE_DRONE_{i}_NAME";
                itemDef.pickupToken = $"FAKE_DRONE_{i}_PICKUP";
                itemDef.descriptionToken = $"FAKE_DRONE_{i}_DESC";         
                itemDef.loreToken = "yes i like femboys";

                itemDef.hidden = true;
                itemDef.canRemove = false;          
                itemDef.tags = [ItemTag.WorldUnique];
                itemDef.deprecatedTier = ItemTier.NoTier;

                var displayRules = new ItemDisplayRuleDict(null);

                ItemAPI.Add(new CustomItem(itemDef, displayRules));

                fakeItemNames.Add(itemDef.name);
            }     
        }
        private void HandleEquipmentDrone(On.RoR2.CharacterMaster.orig_Start orig, CharacterMaster self)
        {
            if (ArtifactEnabled)
            {
                if (self.bodyPrefab.GetComponent<CharacterBody>() == RoR2.RoR2Content.BodyPrefabs.EquipmentDroneBody)
                {
                    EquipmentIndex equipIndex = self.minionOwnership.ownerMaster.inventory.currentEquipmentIndex;
                    if (equipIndex != EquipmentIndex.None)
                    {
                        self.inventory.SetEquipmentIndex(equipIndex, false);
                    }

                }
            }      

            orig(self);
        }

        private DroneDef FindDrone(GameObject interactableObject)
        {
            SummonMasterBehavior s = interactableObject.GetComponent<SummonMasterBehavior>();
            DroneVendorTerminalBehavior t = interactableObject.GetComponent<DroneVendorTerminalBehavior>();

            DroneDef curDrone = null;
            if (s)
            {
                CharacterMaster master = s.masterPrefab.GetComponent<CharacterMaster>();
                curDrone = DroneCatalog.FindDroneDefFromBody(master.bodyPrefab);          
            }
            if (t)
            {
                DroneIndex droneIndex = PickupCatalog.GetPickupDef(t.currentPickup.pickupIndex).droneIndex;
                curDrone = DroneCatalog.GetDroneDef(droneIndex);         
            }

            return curDrone;
        }

        private void HandleDrone(On.RoR2.Interactor.orig_PerformInteraction orig, Interactor self, GameObject interactableObject)
        {
            if (NetworkServer.active && ArtifactEnabled)
            {
                bool flag = FindDrone(interactableObject);

                bool flag2 = interactableObject.GetComponent<DroneVendorTerminalBehavior>();

                bool flag3 = false;
                PurchaseInteraction p = interactableObject.GetComponent<PurchaseInteraction>();
                if (p)
                {
                    flag3 = p && !p.CanBeAffordedByInteractor(self);
                }
                         
                if ((flag || flag2) && !flag3)
                {

                    GameObject cube = RoR2.Artifacts.CommandArtifactManager.commandCubePrefab;

                    GameObject pickerObj = UnityEngine.Object.Instantiate(
                       cube, interactableObject.transform);
                    pickerObj.transform.name = "gofuckyourself";

                    PickupCatalog.itemTierToPickupIndex.TryGetValue(ItemTier.NoTier, out PickupIndex pickupIndex);
                    pickerObj.GetComponent<PickupIndexNetworker>().NetworkpickupState = new UniquePickup(pickupIndex);


                    Rigidbody rb = pickerObj.GetComponent<Rigidbody>();
                    rb.isKinematic = true;

                    foreach (Transform child in pickerObj.transform)
                    {
                        UnityEngine.Object.Destroy(child.gameObject);
                    }


                    PickupPickerController controller = pickerObj.GetComponent<PickupPickerController>();

                    controller.onUniquePickupSelected = new PickupPickerController.UniquePickupUnityEvent();

                    GameObject newPanel = R2API.PrefabAPI.InstantiateClone(controller.panelPrefab, "fuckingpanel");
                    controller.panelPrefab = newPanel;
          

                    DroneDef[] drones = DroneCatalog.droneDefs;

                    List<PickupPickerController.Option> optionList = new List<PickupPickerController.Option>();

                    for (int i = 0; i < drones.Length; i++)
                    {
                        if (drones[i].tier != FindDrone(interactableObject).tier && sameTier.Value)
                            continue;

                        droneToFakeItem.TryGetValue(drones[i].droneIndex, out ItemIndex fakeItem);

                        PickupIndex index = PickupCatalog.FindPickupIndex(fakeItem);
                   
                        optionList.Add(new PickupPickerController.Option
                        {
                            pickup = new UniquePickup(index),
                            available = true,
                        });
                    }

                    controller.SetOptionsServer(optionList.ToArray());

                    NetworkServer.Spawn(pickerObj);

                    controller.OnInteractionBegin(self);


                    controller.onUniquePickupSelected.AddListener((pickup) =>
                    {
                        if (!NetworkServer.active)
                            return;

                        ItemIndex itemIndex = PickupCatalog.GetPickupDef(pickup.pickupIndex).itemIndex;
                        fakeItemToDrone.TryGetValue(itemIndex, out DroneIndex droneIndex);

                        DroneDef drone = DroneCatalog.GetDroneDef(droneIndex);
                        PickupIndex pickupIndex = PickupCatalog.FindPickupIndex(droneIndex);

                        SummonMasterBehavior s = interactableObject.GetComponent<SummonMasterBehavior>();
                        DroneVendorTerminalBehavior t = interactableObject.GetComponent<DroneVendorTerminalBehavior>();
                        if (s)
                        {
                            s.masterPrefab = drone.masterPrefab;
                        }
                        if (t)
                        {
                            t.currentPickup = new UniquePickup(pickupIndex);
                        }

                        UnityEngine.Object.Destroy(pickerObj);

                        orig(self, interactableObject);
                    });
                  
                    return;
                }
            }

            orig(self, interactableObject);

        }
    }
}