using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Inventory : MonoBehaviour, IHasChanged
{

	public List<Item> bagItems; // list of gameobjects or ids or <Item> here
	public List<Item> handItems; //i think int IDs might be best

	public Transform slots;
	public Transform backpackSlots;
	public Transform handSlots;
	public Text inventoryText;

	//public GameObject selectedCharacter;
	public GameObject selectedItem;
	public GameObject toolTip;
	public List<GameObject> allItems = new List<GameObject>();

	public GameObject weaponPrefab;
	public void ClearTooltipAndDeselect(){
		if (toolTip != null) {
				Destroy (toolTip);
		}
		if(selectedItem){
			selectedItem.GetComponentInChildren<ParticleSystem>().Stop();
			selectedItem = null;
		}
	}

	//checks if you have space for loot or buying items
	public bool InventoryIsFull()
	{
		//alternatively you could check partymanager slot 18
		if (bagItems.Count () == 3){// backpackSlots.childCount) {
			return true;
		} else {
			return false;
		}
	}

	public bool HandsAreFull()
	{
		//alternatively you could check partymanager slot 18
		if (handItems.Count () == 1){// backpackSlots.childCount) {
			return true;
		} else {
			return false;
		}
	}


	//Instantiates items into the inventory bag and replaces items[] list with references to the instantiated items
	public IEnumerator UpdateItems(){

		//checks if each item is instantiated yet and if not instantiates it into an empty slot
		for(int j = 0; j < bagItems.Count; j++)
		{
			//check for the item in the inventory
			bool bExists = false;
			int instances = 0;
			for (int i = 0; i < backpackSlots.childCount; i++) 
			{
				InventorySlot slotScript = backpackSlots.GetChild(i).gameObject.GetComponent<InventorySlot>();
				if ((slotScript != null ) && (slotScript.item != null))
				{
					if(ItemsList.AllItemInfo.IndexOf(bagItems[j]) == slotScript.item.GetComponent<Weapon>().item_ID)
					{
						//bExists = true;
						instances++;
					}
				}
			}
			int numberDuplicates = 0;//duplicates in list(not instantiated)
			foreach (Item item in bagItems){
				if(item == bagItems[j]){
					numberDuplicates += 1;
				}
			}

			if(instances >= numberDuplicates){
				bExists = true;
			}
			//instantiate the item in an empty slot
			if (bExists == false)
			{
				foreach (Transform slot in backpackSlots)
				{
					if (slot.childCount == 0)
					{
						GameObject item = Instantiate (weaponPrefab, slot.transform.position, slot.transform.rotation);
						//assign stats to weapons
						AssignWeaponStats(item, ItemsList.AllItemInfo.IndexOf(bagItems[j]));
						//assign coroutines to consumable items
						int id = ItemsList.AllItemInfo.IndexOf(bagItems[j]);
						if(ItemsList.AllItemInfo[id].equippable == false){
							item.GetComponent<CastSpellOnClick>().nameOfCoroutine = ItemsList.AllItemInfo[id].nameOfCoroutine;
							item.GetComponent<CastSpellOnClick>().range = ItemsList.AllItemInfo[id].item_range;
						}
						item.transform.SetParent(slot.transform);
						item.transform.localScale =  new Vector3 (1, 1, 1);
						break;
					}
				}
			}

		}

		//same for hand items
		for(int j = 0; j < handItems.Count; j++)
		{
			//check for the item in the inventory
			bool bExists = false;
			int instances = 0;
			for (int i = 0; i < handSlots.childCount; i++) 
			{
				InventorySlot slotScript = handSlots.GetChild(i).gameObject.GetComponent<InventorySlot>();
				if ((slotScript != null ) && (slotScript.item != null))
				{
					if(ItemsList.AllItemInfo.IndexOf(handItems[j]) == slotScript.item.GetComponent<Weapon>().item_ID)
					{
						//bExists = true;	
						//Debug.Log("Item already exists");
						instances++;//instantiated duplicates
					}
				}
			}
			int numberDuplicates = 0;//duplicates in list(not instantiated)
			foreach (Item item in handItems){
				if(item == handItems[j]){
					numberDuplicates += 1;
				}
			}

			if(instances >= numberDuplicates){
				bExists = true;
			}
			//instantiate the item in an empty slot
			if (bExists == false)
			{
				foreach (Transform slot in handSlots)
				{
					if (slot.childCount == 0)
					{
						GameObject item = Instantiate (weaponPrefab, slot.transform.position, slot.transform.rotation);
						AssignWeaponStats(item, ItemsList.AllItemInfo.IndexOf(handItems[j]));
						item.transform.SetParent(slot.transform);
						item.transform.localScale =  new Vector3 (1, 1, 1);
						break;
					}
				}
			}

		}
		yield return 0;

		////Replace Prefabs in the items list so next time it can detect the item, by comparing the gameobjects in the editor with the item list
		bagItems.Clear ();
		bagItems = GetInventoryItems();
		handItems.Clear ();
		handItems = GetCurrentHeldItems();

		InformationManager.im.UpdatePlayerInfo();
	}

	public List<Item> GetInventoryItems()
	{
		List<Item> children = new List<Item>();
		foreach (Transform child in backpackSlots)
		{
			foreach (Transform grandChild in child)
				children.Add(ItemsList.AllItemInfo[grandChild.gameObject.GetComponent<Weapon>().item_ID]);
		}

		return children;

	}
	//same as GetInventoryItems
	public List<Item> GetCurrentHeldItems()
	{
		List<Item> children = new List<Item>();
		foreach (Transform child in handSlots)
		{
			foreach (Transform grandChild in child)
				children.Add(ItemsList.AllItemInfo[grandChild.gameObject.GetComponent<Weapon>().item_ID]);
		}

		return children;

	}


	#region IHasChanged implementation
	public void HasChanged () 
	{
		InformationManager.im.UpdatePlayerInfo();
	}
	#endregion


	public void AddItem(int itemID){
		#region Deny Conditions
		//check if the item is unique-equipped (can only hold 1)
		if(ItemsList.AllItemInfo[itemID].unique_item ){
			
			if( handItems.Contains(ItemsList.AllItemInfo[itemID]) ||  bagItems.Contains(ItemsList.AllItemInfo[itemID]))
			{
				Debug.Log("You can only carry one of that item.");
				return;
			}
			
		}
		//check if inventory is full
		if(InventoryIsFull()){
			Debug.Log("Inventory is full.");
			return;
		}
		#endregion

		//On Pickup Effects
		if(ItemsList.AllItemInfo[itemID].dropsAllCursedItems){
			DropAllCursed();
		}

		//only apply the effects of weapons while they are in the hand slot
		if(ItemsList.AllItemInfo[itemID].equippable){//if this is a hand item
			if (HandsAreFull ()) {
				bagItems.Add (ItemsList.AllItemInfo[itemID]);						
			}else{
				handItems.Add (ItemsList.AllItemInfo[itemID]);
				ApplyItemEffects(ItemsList.AllItemInfo[itemID], BattleController.bc.player);
			}
		
		}else{
			bagItems.Add (ItemsList.AllItemInfo[itemID]);
			if (ItemsList.AllItemInfo[itemID].equippable == false){
				ApplyItemEffects(ItemsList.AllItemInfo[itemID], BattleController.bc.player);
			}
		}

		
		StartCoroutine (UpdateItems());

	}
 
	public void ApplyItemEffects(Item item, GameObject wielder){
		//Debug.Log("Applying passives");
		ApplyItemPassives(item, wielder);
		/*adding stats on item pickup*/
		UnitController playerUC = wielder.GetComponent<UnitController>();
		if(wielder.name.Contains("Dragon") == false ){
			wielder.GetComponent<CharacterStats>().movement_speed += item.movement_mod;
			playerUC.MovementRange += item.movement_mod;
		}
		wielder.GetComponent<CharacterStats>().accuracy_mod_current += item.item_accuracy_dice_mod;
		wielder.GetComponent<CharacterStats>().defense_mod_current += item.item_defense_dice_mod;
		wielder.GetComponent<CharacterStats>().bonus_weapon_crit_chance += item.item_crit_chance_mod;
		wielder.GetComponent<CharacterStats>().bonus_weapon_damage += item.item_damage_roll_mod;
		wielder.GetComponent<Health>().regenHealthBegin += item.item_regen_health;
		wielder.GetComponent<Health>().regenManaBegin += item.item_regen_mana;

		
		if(item.givesBlindingRage){
			wielder.GetComponent<BuffManager>().BlindingRage();
			Debug.Log("blinding rage applied");
		}

		#region Immunities

		if(item.immunities.Contains("stun")){playerUC.immunities.Add("stun");}

		if(item.immunities.Contains("sleep")){playerUC.immunities.Add("sleep");}

		if(item.immunities.Contains("slow")){playerUC.immunities.Add("slow");}

		if(item.immunities.Contains("root")){playerUC.immunities.Add("root");}

		if(item.immunities.Contains("fall")){playerUC.immunities.Add("fall");}

		if(item.immunities.Contains("knockback")){playerUC.immunities.Add("knockback");}

		if(item.immunities.Contains("frost")){playerUC.immunities.Add("frost");}

		if(item.immunities.Contains("burn")){playerUC.immunities.Add("burn");}

		if(item.immunities.Contains("disease")){playerUC.immunities.Add("disease");}

		if(item.immunities.Contains("bleed")){playerUC.immunities.Add("bleed");}

		if(item.immunities.Contains("damage")){playerUC.immunities.Add("damage");}

		#endregion

		//AI stuff
		if(playerUC.aiInfo.team == 0){
			return;
		}
		//add spells cast by items to the AIs list of known spells
		if(item.item_name == "Belt of the Forest"){
			playerUC.knownSpells.Insert(0, "Serrating Vines");
		}
		if(item.item_name == "Belt of Vivid Dreams"){
			playerUC.knownSpells.Insert(0, "Vivid Dreams");
		}
		if(item.item_name == "Blink Stone"){
			playerUC.knownSpells.Insert(0, "Blink Stone");
		}
		if(item.item_name == "Boots of Time Warp"){
			playerUC.knownSpells.Insert(0, "Time Warp");
		}
		if(item.item_name == "Blood Boiling Potion"){
			playerUC.knownSpells.Insert(0, "Blood Boiling Potion");
		}
		if(item.item_name == "Health Fruit"){
			playerUC.knownSpells.Insert(0, "Health Potion");
		}
	}
	
	public void RemoveItem(GameObject item, bool destroyWhenDone = true){
		int id = item.GetComponent<Weapon>().item_ID;
		if(bagItems.Contains(ItemsList.AllItemInfo[id])){
			bagItems.Remove (ItemsList.AllItemInfo[id]);
			if (ItemsList.AllItemInfo[id].equippable == false){
				RemoveItemEffects(ItemsList.AllItemInfo[id], BattleController.bc.player);
			}
		} 
		else if(handItems.Contains(ItemsList.AllItemInfo[id])){
			handItems.Remove (ItemsList.AllItemInfo[id]);
			RemoveItemEffects(ItemsList.AllItemInfo[id], BattleController.bc.player);
		}

		if(destroyWhenDone){
			Destroy(item);
		}
		StartCoroutine (UpdateItems());
	}

	public void RemoveItemEffects(Item item, GameObject wielder){
		UnitController playerUC = wielder.GetComponent<UnitController>();

		RemoveItemPassives(item, wielder);
		/*adding stats on item pickup*/
		wielder.GetComponent<CharacterStats>().movement_speed -= item.movement_mod;
		playerUC.MovementRange -= item.movement_mod;
		wielder.GetComponent<CharacterStats>().accuracy_mod_current -= item.item_accuracy_dice_mod;
		wielder.GetComponent<CharacterStats>().bonus_weapon_crit_chance -= item.item_crit_chance_mod;
		wielder.GetComponent<CharacterStats>().bonus_weapon_damage -= item.item_damage_roll_mod;
		
		if(item.givesBlindingRage){
			//checks to make sure you do not have a 2nd item with blinding rage before removing it

			List<Item> allItems = new List<Item>();
			if(wielder == BattleController.bc.player){
				allItems.AddRange(handItems);
				allItems.AddRange(bagItems);

			}
			else{
				allItems.AddRange(playerUC.aiInfo.handItems);
				allItems.AddRange(playerUC.aiInfo.bagItems);
			}

			if(allItems.Count > 0){
				//if there is not another item with blinding rage after removing the item, 
				//then we can remove the blinding rage debuff
				if(allItems.Find(x => x.givesBlindingRage == true) != null){
					wielder.GetComponent<BuffManager>().RemoveBlindingRage();
				}
			}
		}


		#region Immunities

		//checks for any other conditions where you will retain an immunity after removing and item
		//such as Job passives or other items in the bag

		if(item.immunities.Contains("stun")){playerUC.immunities.Remove("stun");}

		if(item.immunities.Contains("sleep")){playerUC.immunities.Remove("sleep");}

		if(item.immunities.Contains("slow")){playerUC.immunities.Remove("slow");}

		if(item.immunities.Contains("root")){playerUC.immunities.Remove("root");}

		if(item.immunities.Contains("fall")){playerUC.immunities.Remove("fall");}

		if(item.immunities.Contains("knockback")){playerUC.immunities.Remove("knockback");}

		if(item.immunities.Contains("frost")){playerUC.immunities.Remove("frost");}

		if(item.immunities.Contains("burn")){playerUC.immunities.Remove("burn");}

		if(item.immunities.Contains("disease")){playerUC.immunities.Remove("disease");}

		if(item.immunities.Contains("bleed")){playerUC.immunities.Remove("bleed");}

		if(item.immunities.Contains("damage")){playerUC.immunities.Remove("damage");}

		#endregion

		//AI stuff
		if(playerUC.aiInfo.team == 0){
			return;
		}
		if(item.item_name == "Belt of the Forest"){
			playerUC.knownSpells.Remove("Serrating Vines");
		}
		if(item.item_name == "Belt of Vivid Dreams"){
			playerUC.knownSpells.Remove("Vivid Dreams");
		}
		if(item.item_name == "Blink Stone"){
			playerUC.knownSpells.Remove("Blink Stone");
		}
		if(item.item_name == "Boots of Time Warp"){
			playerUC.knownSpells.Remove("Time Warp");
		}
		if(item.item_name == "Blood Boiling Potion"){
			playerUC.knownSpells.Remove("Blood Boiling Potion");
		}
		if(item.item_name == "Health Fruit"){
			playerUC.knownSpells.Remove("Health Potion");
		}
	}

	public void ApplyItemPassives(Item item, GameObject wielder){
		if(item.item_passives != null && item.item_passives.Length > 0){
			foreach(string passive in item.item_passives){
				JobDescriptions.ApplyPassiveEffect(passive, wielder);
			}
		}
	}

	public void RemoveItemPassives(Item item, GameObject wielder){
		if(item.item_passives != null && item.item_passives.Length > 0){
			foreach(string passive in item.item_passives){
				JobDescriptions.RemovePassiveEffect(passive, wielder);
			}
		}
	}

	//assigns stats to the generic weapon prefab. DOES NOT apply effects to the player/wielder
	public void AssignWeaponStats(GameObject item, int item_ID){
		Weapon script = item.GetComponent<Weapon>();
		script.item_ID = item_ID;
		script.item_name = ItemsList.AllItemInfo[item_ID].item_name;
		script.item_description = ItemsList.AllItemInfo[item_ID].item_description;
		script.item_range = ItemsList.AllItemInfo[item_ID].item_range;
		script.unique_item = ItemsList.AllItemInfo[item_ID].unique_item;
		script.equippable_item = ItemsList.AllItemInfo[item_ID].equippable;
		script.cursed_item = ItemsList.AllItemInfo[item_ID].cursed;
		script.item_targeting_type = ItemsList.AllItemInfo[item_ID].item_targeting_type;
		script.item_damage_roll_count = ItemsList.AllItemInfo[item_ID].item_damage_roll_count;
    	script.item_damage_roll_sides = ItemsList.AllItemInfo[item_ID].item_damage_roll_sides;
		script.item_damage_roll_mod = ItemsList.AllItemInfo[item_ID].item_damage_roll_mod;
		script.item_crit_chance = ItemsList.AllItemInfo[item_ID].item_crit_chance;
		script.item_crit_effect_chance = ItemsList.AllItemInfo[item_ID].item_crit_effect_chance;
		script.crit_effect_name = ItemsList.AllItemInfo[item_ID].crit_effect_name;
		script.movement_mod = ItemsList.AllItemInfo[item_ID].movement_mod;
		
		//you cannot add these things to a monsters weapon(the gameObject of the weapon is the monster itself)
		bool isAI = false;
		if(item.GetComponent<UnitController>()){
			if(item.GetComponent<UnitController>().aiInfo != null){
				isAI = true;
			}
		}
		if(item.tag != "Monster" && !isAI){
			item.transform.GetChild(0).GetComponent<Image>().sprite = ItemsList.il.allSprites[item_ID];
			item.GetComponent<CastSpellOnClick>().actionsRequired = ItemsList.AllItemInfo[item_ID].actionsRequired;
			
			if(ItemsList.AllItemInfo[item_ID].equippable){
				item.GetComponent<CastSpellOnClick>().nameOfCoroutine = "AttackWithWeapon";
				item.GetComponent<CastSpellOnClick>().range = script.item_range;
			}
		}
	}


	static public Collider[] reusableNeighborList = new Collider[7];
	public void DropItemOnFloor(GameObject item, bool allowCurseDrop = false){
		if(item.GetComponent<Weapon>().cursed_item && allowCurseDrop == false){
			InformationManager.im.info_text.text = "You cannot drop a cursed item.";
			return;
		}
		RemoveItem(item);

		GameObject player = BattleController.bc.player;
		List<MapHexTile> emptySpaces = new List<MapHexTile>();
		int range = 1;
		//find the nearest empty space available(even if its a mile away)
		while(emptySpaces.Count == 0 && range < 200){
			int numFound = Physics.OverlapSphereNonAlloc(player.transform.position, range, reusableNeighborList);
			for (int i = 0; i < numFound; i++) {
				if(reusableNeighborList[i].gameObject.GetComponent<MapHexTile>().occupant == null){
					emptySpaces.Add(reusableNeighborList[i].gameObject.GetComponent<MapHexTile>());
				}
			}
			range++;
		}

		//if no empty spaces nearby, skip
		if(emptySpaces.Count != 0){
			int rando = Random.Range(0,emptySpaces.Count);
			GameObject droppedItem = (GameObject)Instantiate(MapGenerator.mg.rarePickupPrefabs[0], emptySpaces[rando].transform.parent.position, Quaternion.identity);
			emptySpaces[rando].occupant = droppedItem;
			droppedItem.GetComponent<Pickup>().id = item.GetComponent<Weapon>().item_ID;
			droppedItem.transform.SetParent(MapGenerator.mg.gameObject.transform);
		}
	}

	public void DropAllCursed(bool atBase = false){
		//equipped
		if(handItems.Count > 0){
			if(handItems[0].cursed){
				DropItemOnFloor(handSlots.parent.GetChild(2).GetChild(0).gameObject, true);
			}
		}
		//bag
		if(bagItems.Count > 0){
			foreach(Transform slot in backpackSlots){
				if(slot.childCount > 0){
					GameObject heldItem = slot.GetChild(0).gameObject;
					if(heldItem != null && heldItem.GetComponent<Weapon>().cursed_item){
						DropItemOnFloor(heldItem, true);
					}
				}
			}
		}
	}

	//Drop items on death
	public void DropAllItems(){
		//equipped
		if(handItems.Count > 0){
			DropItemOnFloor(handSlots.parent.GetChild(2).GetChild(0).GetChild(0).gameObject, true);
		}
		//bag
		if(bagItems.Count > 0){
			foreach(Transform slot in backpackSlots){
				if(slot.childCount > 0){
					GameObject heldItem = slot.GetChild(0).gameObject;
					if(heldItem != null ){
						DropItemOnFloor(heldItem, true);
					}
				}
			}
		}
	}
}


namespace UnityEngine.EventSystems {
	public interface IHasChanged : IEventSystemHandler {
		void HasChanged();
	}
}
