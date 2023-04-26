
//Note to the reader: This controls all the AI units in a turn based game, where the game field is a hexagon (or square) grid.
//They can navigate effectively, pick up and use a large range of items, use spells tactically and more.
//Several functions/methods were removed for readability


using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class AIController : MonoBehaviour
{
   
	
    public Inventory inventory;

    public List<Transform> team1Spawnpoints;
    public List<Transform> team2Spawnpoints;

    int enemyActionsPointsSpent;// 2 actions are allowed per turn
    public GameObject mCamera;
    public List<MapHexTile> path;
    public bool showAIMovement;
    
    
    [HideInInspector]
    public GameObject spawnedIndicator;
    
    //static self reference, so i dont have to link scripts to eachother
    public static AIController aic;
    
    void Awake(){
        aic = this;
    }
    
    public void SpawnAIs(){
        //spawn AI team mates
        for(int i = 0; i < 3; ++i){
            CreateAIPlayer(1);
        }
        //spawn AI enemies
         for(int i = 0; i < 4; ++i){
            CreateAIPlayer(2);
        }
    }
	
    public void CreateAIPlayer(int team){
    
        //decide which team mate this is to decide spawn point
        int playerNumber = team == 1 ? playerNumber = BattleController.bc.team1Players.Count + 1 : playerNumber = BattleController.bc.team2Players.Count + 1;
        Transform spawnHere = team == 1 ? team1Spawnpoints[playerNumber] : team2Spawnpoints[playerNumber];
        
        //spawn AI characters
	GameObject player = (GameObject)Instantiate(BattleController.bc.playerPrefab, spawnHere.position, Quaternion.identity);
	player.GetComponent<UnitController>().occupiedSpace = spawnHere.GetComponentInChildren<MapHexTile>();
	spawnHere.GetComponentInChildren<MapHexTile>().occupant = player;
	player.transform.SetParent(MapGenerator.mg.transform);
	player.transform.SetAsFirstSibling();
	
	//references
	CharacterStats stats = player.GetComponent<CharacterStats>();
	Health health = player.GetComponent<Health>();
	
	//assign an RPG job such as Warrior, Archer or Mage
	Job my_job = JobDescriptions.AllJobs[Random.Range(0,JobDescriptions.AllJobs.Count)];
	stats.job_name = my_job.job_name;

	//assign stats
	stats.movement_speed = my_job.movement_speed;
	player.GetComponent<UnitController>().MovementRange = my_job.movement_speed;
	health.maximumHealth = 15 + my_job.health_mod_job + (Random.Range(1,  (my_job.health_mod_sides + 1) * my_job.health_mod_rolls));
	health.maximumMana = 15 + my_job.mana_mod_job + (Random.Range(1,  (my_job.mana_mod_sides + 1) * my_job.mana_mod_rolls));
	health.regenHealthBegin = my_job.health_regen_start;
	health.regenManaBegin = my_job.mana_regen_start;
	stats.defense_base = 10 + my_job.defense_profession_mod;
	stats.defense_total = stats.defense_base;
	stats.accuracy_base = 10 + my_job.accuracy_profession_mod;
	stats.accuracy_total = stats.accuracy_base;
        stats.defense_total += stats.defense_mod_current + (Random.Range(1, (my_job.defense_dice_sides + 1) * (my_job.defense_dice_rolls + stats.defense_roll_count_mod)));
	stats.accuracy_total += stats.accuracy_mod_current + (Random.Range(1, (my_job.accuracy_dice_sides + 1) * (my_job.accuracy_dice_rolls + stats.accuracy_roll_count_mod)));

        //Assign Spells
        player.GetComponent<UnitController>().knownSpells = my_job.spells_known;

	//apply passive abilities
	foreach(string passive in my_job.level_one_passive_abilities){
		JobDescriptions.ApplyPassiveEffect(passive, player);
	}
	
	//info that allows the AI logic to work
        AIPlayerInfo info = new AIPlayerInfo();
        info.team = team;
        info.jobID = JobDescriptions.AllJobs.IndexOf(my_job);
	player.GetComponent<UnitController>().aiInfo = info;
	
        //give default weapon
        player.gameObject.AddComponent<Weapon>();
	inventory.AssignWeaponStats(player.gameObject, my_job.default_weapon_1);
	//put the weapon in a non-visible inventory the AI has
	AddItemToAIInventory(my_job.default_weapon_1, player.GetComponent<UnitController>());
		

        //color AIs by team
        if(team == 1){
            foreach(Renderer r in player.GetComponentsInChildren<Renderer>()){
                r.material.color = Color.blue;
            }
        }else{
            foreach(Renderer r in player.GetComponentsInChildren<Renderer>()){
                r.material.color = Color.red;
            }
        }

	//enable the outfit for the selected job, all outfits are parented under the AI character prefab
	UnitController.Outfit outfit = player.GetComponent<UnitController>().outfits.Find(x => x.name == my_job.job_name);
	foreach(GameObject piece in outfit.pieces){
		piece.SetActive(true);
	}

   }
    
    
    
    
    //the main coroutine that runs the AIs
    public IEnumerator AIEnemyTurn(){
    	//can toggle viewing the AIs turns or skipping the animations
	bool showNonPlayerTurns = BattleController.bc.showNonPlayerTurns;	
	yield return new WaitForSeconds (0.3f);
	
        //list all the players in the match so we can give instructions to all AIs
	List<GameObject> allPlayers = new List<GameObject>();
        foreach (GameObject enemy in BattleController.bc.team1Players){
             allPlayers.Add(enemy);
        }
        foreach (GameObject enemy in BattleController.bc.team2Players){
             allPlayers.Add(enemy);
        }
	
	//Loop for each AI to instruct 
	foreach (GameObject enemy in allPlayers.Where(x => x.GetComponent<UnitController>().aiInfo.team != 0)){
		if(enemy == null){
			continue;
		}
		//respawning (they get 1 turn dead for each time they have died so far, 3 deaths = 3 turns dead)
		UnitController enemyUC = enemy.GetComponent<UnitController>();
		if(enemy.GetComponent<Health>().currentHealth <= 0){
			enemyUC.timeoutCounter += 1;
			//respawn after time has passed
			if(enemyUC.timeoutCounter > enemyUC.totalDeaths){
				enemy.GetComponent<Health>().currentHealth = enemy.GetComponent<Health>().maximumHealth;
				enemy.GetComponent<Health>().currentMana = enemy.GetComponent<Health>().maximumMana;
				enemyUC.timeoutCounter = 0;

				Renderer[] renderers = enemy.transform.GetComponentsInChildren<Renderer>();
				int index = 0;
				//Sets all the characters materials back to their original material, since they become transparent when dead
				foreach (Renderer r in renderers){
					r.material = enemy.GetComponent<Health>().ogMaterials[index];
					index++;
				}

			}else{
				continue;
			}
		}
		
		enemyActionsPointsSpent = 0;

		enemy.GetComponent<Health>().RegenBeginTurn();
		enemy.GetComponent<BuffManager>().Tick();
		enemyUC.hasMoved = false;
		enemyUC.spacesWalked = 0;
		enemyUC.hasAttacked = false;
		enemyUC.hasSwappedWeapons = false;
		enemyUC.hasUsedItem = false;

		yield return new WaitForEndOfFrame();
		
		//if stunned or asleep, they cannot take their turn, buffs and regen ticks and it goes to the next AI
		if(enemyUC.stunned || enemyUC.asleep){
			enemy.GetComponent<BuffManager>().Tick(true);
			enemy.GetComponent<Health>().RegenEndTurn();
			continue;
		}

		//focus camera on the AI
		if(BattleController.bc.showNonPlayerTurns){
			mCamera.GetComponent<CameraController>().FocusCamera(enemy);
		}


		#region  Walking/Movement
		
		//the Move function (down below) uses a complex pathfinding system to navigate toward any objective
		yield return StartCoroutine(Move(enemyUC, enemy));
		//the "yield return" allows the whole sequence to play out before going to the next section
		
		//checks to see if we reached our objective, and if not, later it will use items and spells to move further
		bool destinationReached = true;
		if(path != null){
			if(path.IndexOf(enemyUC.occupiedSpace) < path.Count -1){
				destinationReached = false;
			}
		}
		#endregion


		#region  Use Items
		
		if(enemyUC.objective == null){
			//sets the objective to the closest enemy OR item based on Vector3.Distance()
			enemyUC.objective = NearestHostileEnemy(enemy, true);
		}
		string objective_tag = enemyUC.objective == null ? "" : enemyUC.objective.tag;

		string consumableItems = "Vivid Dreams, Health Potion, Blink Stone, Blood Boiling Potion, Boots of Time Warp";

		string chosenItem = "";
		//the items that can be activated are added to the holders known spells
		foreach(string spellKnown in enemy.GetComponent<UnitController>().knownSpells){
			if(consumableItems.Contains(spellKnown)){

				//if we have not reached the target after walking try to use blink stone
				if(destinationReached == false){
					if(spellKnown == "Blink Stone"){
						chosenItem = spellKnown;
					}
				}
				//use buff potions if we dont already have the effect
				if(spellKnown == "Blood Boiling Potion" && enemy.GetComponent<BuffManager>().currentBuffs.Contains("Boiling Blood") == false){
					chosenItem = spellKnown;
				}
				//Mana recovery items, used if low mana
				if(enemy.GetComponent<Health>().currentMana < enemy.GetComponent<Health>().maximumMana/5){
					if(spellKnown == "Vivid Dreams"){
						chosenItem = spellKnown;
						//possibly check for enemies nearby
					}
				}
				//low health tactics
				Health health = enemy.GetComponent<Health>();
				if (health.currentHealth <= health.maximumHealth/3){
					//use health potions
					if(spellKnown == "Health Potion"){
						chosenItem = spellKnown;
					}
					//use boots of time warp
					if(spellKnown == "Time Warp"){
						chosenItem = spellKnown;
					}
				}


			}
		}
		
		//sets selectedCaster of spellsmanager, just so we know who is using something
		SpellsManager.sm.ShowSpellRange(enemy, 0,  "range");
		yield return new WaitForEndOfFrame();
		SpellsManager.sm.Deselect();

		GameObject targetInRange = enemy;
		//blink stone space targeting (the blink stone teleports you 8 spaces away)
		if(chosenItem == "Blink Stone"){
			//uses the path set by movement earlier on
			int newIndex = path.IndexOf(enemyUC.occupiedSpace) + 8;
			//clamp to end of path
			if(newIndex > path.Count -1){
				newIndex = path.Count -1;
			}
			targetInRange = path[newIndex].gameObject;
		}

		//after selecting our item, and our target execute the effect
		if(chosenItem != "" && targetInRange != null){
			string targetName = targetInRange == enemy?  "self" : targetInRange.name;
			string spellNameWithoutSpaces = chosenItem.Replace(" ", "");
			InformationManager.im.AddToCombatLog("\n <color=cyan>" + enemy.name + " Uses " + chosenItem + " on " + targetName + "</color>");
			yield return SpellsManager.sm.StartCoroutine(spellNameWithoutSpaces, new object[]{targetInRange});
			if(consumableItems.Contains(chosenItem)){
				//remove item
				int itemID = -1;
				if(chosenItem == "Vivid Dreams"){
					itemID = 11;
				}
				if(chosenItem == "Blink Stone"){
					itemID = 14;
				}

				if(itemID != -1){
					RemoveAIItem(ItemsList.AllItemInfo[itemID], enemy);
				}
			}
		}

		//after using an item we need to check if we are asleep or stunned now, and skip the rest of the turn accordingly
		if(enemy == null || enemy.GetComponent<Health>().currentHealth <= 0){
			continue;
		}
		if(enemyUC.stunned || enemyUC.asleep){
			enemy.GetComponent<BuffManager>().Tick(true);
			enemy.GetComponent<Health>().RegenEndTurn();
			continue;
		}

		#endregion


		#region Cast Spells

		if(enemyActionsPointsSpent < 2 && enemy.GetComponent<Health>().currentMana > 7 &&  objective_tag != enemy.tag && objective_tag != "Pickup"){

			//decide which spell the enemy will cast
	
			//define the spells enemies can cast which have some priority over others
			string healingSpells = "Heal, Healing Rain, Divine Intervention";
			string debuffSpells = "Demoralizing Roar, Contagion, Exposure, Serrating Vines, Rapid Virus";
			string buffSpells = "Spike Shield, Blood Rage";
			string imbueSpells = "Hobbling Strike, Fiery Imbue, Crushing Blow, Blessing";

			//direct damage not defined and is lowest priority

			int chosenSpellID = -1;
			int currentPriority = 100;
			foreach(string spellKnown in enemy.GetComponent<UnitController>().knownSpells){
				Spell spell_class = SpellList.AllSpells.Find(x => x.spell_name == spellKnown);
				//sometimes the AIs known spells will be a spell cast by item and have no Spell class
				int spellID = spell_class == null? -1 : SpellList.AllSpells.IndexOf(spell_class);

				if (spellID == enemyUC.lastSpellCast || spell_class == null){
					continue;
				}

				if(enemy.GetComponent<Health>().currentMana >= BattleController.bc.ManaCostBySpellLevel(SpellList.AllSpells[spellID].spell_level)){

					//priority 1
					if(healingSpells.Contains(SpellList.AllSpells[spellID].spell_name) ){
						//determine if anyone is dying/low health within range
						GameObject lowestAlly = BattleController.bc.LowestHealthWithinRange(SpellList.AllSpells[spellID].spell_range, enemy);

						if(SpellList.AllSpells[spellID].spell_name == "Divine Intervention"){
							if(enemy.GetComponent<Health>().currentHealth > enemy.GetComponent<Health>().maximumHealth/2){
								if(lowestAlly != null && lowestAlly.GetComponent<Health>().currentHealth < Mathf.RoundToInt(lowestAlly.GetComponent<Health>().maximumHealth * 0.30f)){
									chosenSpellID = spellID;
									currentPriority = 1;
								}
							}
						}

						else if(lowestAlly != null && lowestAlly.GetComponent<Health>().currentHealth < Mathf.RoundToInt(lowestAlly.GetComponent<Health>().maximumHealth * 0.75f)){
							chosenSpellID = spellID;
							currentPriority = 1;

						}
					}
					//priority 2
					else if(imbueSpells.Contains(SpellList.AllSpells[spellID].spell_name)){
						if(enemy.GetComponent<Weapon>() == true && enemy.GetComponent<Weapon>().isImbued == false){
							if(currentPriority > 2){
								chosenSpellID = spellID;
								currentPriority = 2;
							}
						}
					}
					//priority 3
					else if(buffSpells.Contains(SpellList.AllSpells[spellID].spell_name)){
						//if we already have spike shield, dont use it
						if(enemy.GetComponent<BuffManager>().currentBuffs.Contains("Spike Shield") == false){
							if(currentPriority > 3){
								chosenSpellID = spellID;
								currentPriority = 3;
							}
						}


					}
					//priority 4
					else if(debuffSpells.Contains(SpellList.AllSpells[spellID].spell_name) ){

						//contagion cast for refresh
						if(SpellList.AllSpells[spellID].spell_name == "Contagion" && enemyUC.objective.GetComponent<BuffManager>().currentBuffs.Contains("Disease") == false){
							if(currentPriority > 4){
									chosenSpellID = spellID;
									currentPriority = 4;
							}
						}
						else if(SpellList.AllSpells[spellID].spell_name == "Demoralizing Roar" || SpellList.AllSpells[spellID].spell_name == "Exposure"){
							//only cast if the target doesnt have the debuff
							if(enemyUC.objective.GetComponent<BuffManager>().currentBuffs.Contains("Demoralizing Roar") == false
								&& enemyUC.objective.GetComponent<BuffManager>().currentBuffs.Contains("Exposure") == false){
								if(currentPriority > 4){
										chosenSpellID = spellID;
										currentPriority = 4;
								}
							}
						}

						else if(SpellList.AllSpells[spellID].spell_name == "Serrating Vines" && enemyUC.objective.GetComponent<BuffManager>().currentBuffs.Contains("Root") == false){
							if(currentPriority > 4){
								chosenSpellID = spellID;
								currentPriority = 4;
							}
						}
					}
					//priority 5
					else /*direct attacks*/{

						if(spellKnown == "Heroic Leap"){
							if(destinationReached == true){
								continue;
							}
						}


						if(currentPriority > 5){
								chosenSpellID = spellID;
								currentPriority = 5;

						}

						//if it is the same priority as the last spells
						if(currentPriority == 5){
							//use the one with higher spell level
							if(SpellList.AllSpells[spellID].spell_level > chosenSpellID){
								chosenSpellID = spellID;
							}
						}
					}
				}
			}

			if (chosenSpellID == -1) {
				enemyUC.lastSpellCast = -1;
			}
			else{
				//show the range of the chosen spell (to the AI) and considers all possible target locations
				SpellsManager.sm.ShowSpellRange(enemy, SpellList.AllSpells[chosenSpellID].spell_range,  SpellList.AllSpells[chosenSpellID].range_type);

	   			int targeting_type = SpellList.AllSpells[chosenSpellID].spell_targeting_type;
				GameObject unitInRange = null;
				foreach (GameObject tile in SpellsManager.sm.spacesInRange){
					if(tile.GetComponent<MapHexTile>().occupant != null && tile.GetComponent<MapHexTile>().occupant.GetComponent<Pickup>() == null){
						
						//check if the spell can be cast on this target
						bool correct_target = false;

						string occupantTag = tile.GetComponent<MapHexTile>().occupant.gameObject.tag;

						//targeting types
						//0 = ally & self, 1 = self, 2 = ally only , 3 = enemy only, 4 = monster only,
						//5 = enemy & monster  6= any target type.  

						    //allys
						    if(targeting_type == 0){
							if(occupantTag == enemy.tag){
							    correct_target = true;
							}
						    }
						    //self
						    if(targeting_type == 1){
							if(tile.GetComponent<MapHexTile>().occupant == enemy){
							    correct_target = true;
							}
						    }
						    //only allies
						    if(targeting_type == 1){
							if(tile.GetComponent<MapHexTile>().occupant != enemy && occupantTag == enemy.tag){
							    correct_target = true;
							}
						    }
						    //only enemies
						    if(targeting_type == 3){
							if(occupantTag != enemy.tag && occupantTag != "Monster"){
							    correct_target = true;
							}
						    }
						    //only Monsters
						    if(targeting_type == 4){
							if(occupantTag == "Monster"){
							    correct_target = true;
							}
						    }
						    //enemies + monsters
						    if(targeting_type == 5){
							if(occupantTag != enemy.tag){
							    correct_target = true;
							}
						    }

						    if (targeting_type == 6){
							correct_target = true;
						    }

						    //prevent double imbue
						    if(imbueSpells.Contains(SpellList.AllSpells[chosenSpellID].spell_name)){
						    	    GameObject target = tile.GetComponent<MapHexTile>().occupant;

							    //check if target is imbued
							    if(target == BattleController.bc.player){
								    if(inventory.handSlots.GetChild(0).GetChild(0).GetComponent<Weapon>().isImbued){
									correct_target = false;
								    }
							    }
							    else if(target.GetComponent<Weapon>().isImbued){
								correct_target = false;
							    }
						    }

						if(correct_target){

							unitInRange = tile.GetComponent<MapHexTile>().occupant;
						}
					}
					//targeting spaces
					//i guess this will just target closeby spaces
					else if( targeting_type == 7){
						unitInRange = tile;
					}
				}
				if(showAIMovement && showNonPlayerTurns) yield return new WaitForSeconds (0.3f);

				SpellsManager.sm.Deselect();

				//make heroic leap target a tile 
				if(chosenSpellID ==  SpellList.AllSpells.IndexOf(SpellList.AllSpells.Find(x => x.spell_name == "Heroic Leap")) ){
					if(enemyUC.occupiedSpace == path[path.Count -1]){
						unitInRange = null;
					}else{
						int newIndex = path.IndexOf(enemyUC.occupiedSpace) + 8;
						//clamp to end of path
						if(newIndex > path.Count -1){
							newIndex = path.Count -1;
						}
						unitInRange = path[newIndex].gameObject;
					}
				}

				if(unitInRange != null){
					if(unitInRange != enemy){
						enemy.transform.LookAt(unitInRange.transform.position);
						enemy.transform.eulerAngles = new Vector3(0,enemy.transform.eulerAngles.y,0);
					}
					//run the correct spell coroutine
					string spellNameWithoutSpaces = SpellList.AllSpells[chosenSpellID].spell_name.Replace(" ", "");

					//spend mana
					int manaCost = BattleController.bc.ManaCostBySpellLevel(SpellList.AllSpells[chosenSpellID].spell_level);//SpellList.AllSpells[chosenSpellID].spell_level * 8;
					enemy.GetComponent<Health>().SpendMana(manaCost);
					//Debug.Log ( manaCost + " mana was spent");
					enemyUC.lastSpellCast = chosenSpellID;
					//cast spell
					//if(showNonPlayerTurns){
					InformationManager.im.AddToCombatLog("\n <color=orange>" + enemy.name + " Casts " + spellNameWithoutSpaces + " on " + unitInRange.name + "</color>");
					yield return SpellsManager.sm.StartCoroutine(spellNameWithoutSpaces, new object[]{unitInRange});
					//}else{
					//	SpellsManager.sm.StartCoroutine(spellNameWithoutSpaces, new object[]{unitInRange});
					//}
					//spell coroutines handle their own action point spending

				}
			}

			if(showAIMovement  && showNonPlayerTurns) yield return new WaitForSeconds (0.3f);
		}

		//after using a spell we need to check if we are asleep, stunned or dead now, and skip the rest of the turn accordingly
		if(enemy == null || enemy.GetComponent<Health>().currentHealth <= 0){
			continue;
		}
		if(enemyUC.stunned || enemyUC.asleep){
			enemy.GetComponent<BuffManager>().Tick(true);
			enemy.GetComponent<Health>().RegenEndTurn();
			continue;
		}
		#endregion

		#region  Weapon Attack
		if(enemyUC.objective == null){
			enemyUC.objective = NearestHostileEnemy(enemy, true);
		}

		if(enemyActionsPointsSpent < 2 && enemy.GetComponent<Weapon>() != null){
			SpellsManager.sm.ShowSpellRange(enemy,enemy.GetComponent<Weapon>().item_range,"range");
			GameObject playerUnitInWeaponRange = null;
			foreach (GameObject tile in SpellsManager.sm.spacesInRange){
	  			GameObject occupant = tile.GetComponent<MapHexTile>().occupant;
				if(occupant != null){
					if(occupant.tag != enemy.tag && occupant.GetComponent<Pickup>() == false){
						playerUnitInWeaponRange = tile.GetComponent<MapHexTile>().occupant;
					}
				}
			}
			if(showAIMovement) yield return new WaitForSeconds (0.3f);
			SpellsManager.sm.Deselect();

			//attack			
			if(playerUnitInWeaponRange != null){
				SpellsManager.sm.Deselect();

				//attackwithweapon has its own animation skipping
				yield return SpellsManager.sm.StartCoroutine("AttackWithWeapon",new object[] {playerUnitInWeaponRange});

				enemyActionsPointsSpent += 1;
			}

		}
		#endregion

		if(showAIMovement  && showNonPlayerTurns) yield return new WaitForSeconds (0.3f);	

		if(enemyUC.occupiedSpace == enemyUC.startTile && enemyUC.objective == null){
				enemyUC.StartCoroutine(enemyUC.MonsterRegen(enemy.GetComponent<Health>().maximumHealth/10));
		}

		enemy.GetComponent<Health>().RegenEndTurn();
		enemy.GetComponent<BuffManager>().Tick(true);
		enemyActionsPointsSpent = 0;
		if(enemy != null){
			enemyUC.spacesWalked = 0;
		}

	}
        
        
		//start player turn
		yield return new WaitForEndOfFrame();
		BattleController.bc.StartPlayerTurn();
	}
	


	//Movement, used above
	public IEnumerator Move(UnitController enemyUC, GameObject enemy){
		bool showNonPlayerTurns = BattleController.bc.showNonPlayerTurns;

		//set enemy objective
		enemyUC.objective = NearestHostileEnemy(enemy, true);//also searches for nearby items

		//stand still if there is no objective available
		if(enemyUC.objective == enemyUC.occupiedSpace.gameObject){
			path.Clear();
			path.Add(enemyUC.occupiedSpace);
			yield break;
		}

		bool objectiveIsItem = enemyUC.objective.GetComponent<Pickup>() == null ? false: true;
		int itemRange = enemy.GetComponent<Weapon>().item_range;

		if(enemyUC.rooted == false && enemyUC.spacesWalked < enemyUC.MovementRange){
				enemyUC.ShowWalkableSpaces();
				MapHexTile chosenSpace = enemyUC.occupiedSpace;


				//move toward an objective
				if(enemyUC.objective != null){
					//find the map tile
					MapHexTile locationOfObjective = enemyUC.objective.GetComponent<MapHexTile>()? enemyUC.objective.GetComponent<MapHexTile>(): BattleController.bc.NearestMapTile(enemyUC.objective);
					
					//path to objective
					path = PathfindingHandler.FindPath(enemyUC.occupiedSpace, locationOfObjective);
					
					if(path == null){
						//the path is blocked
						Debug.Log(enemy.gameObject.name + "has no path to its target");
					}else{
		
						path.Reverse();

						//remove last tile from the path if occupied 
						if(enemyUC.objective.GetComponent<UnitController>()){
							path.Remove(path[path.Count -1]);
						}
						
						//decide how close to move
						if(path.Count > 0){
							
							int firstEmpty = 0;
							//furthest open space index
							foreach(MapHexTile tile in path){
								if(tile.occupant == null || tile.occupant.GetComponent<Pickup>()){
									firstEmpty = path.IndexOf(tile);
								}
							}

							//subtract weapon range from index to move further away
							if(objectiveIsItem == false){
								firstEmpty -= (itemRange -1);//might be causing path problems

								//short paths and long ranges will be negative
								if(firstEmpty < 0){
									firstEmpty = 0;
								}
							}


							int toMove = firstEmpty;
							if(enemyUC.MovementRange - enemyUC.spacesWalked < firstEmpty + 1){
								toMove = (int)enemyUC.MovementRange - enemyUC.spacesWalked;
							}
							chosenSpace = path[toMove];
						}
					}
				}
				
				
				if(showAIMovement && showNonPlayerTurns){
                    yield return new WaitForSeconds (0.3f);
                } 

				enemyUC.Deselect();

				GameObject temp = chosenSpace.occupant;

				//grab pickups
				if(chosenSpace.occupant && chosenSpace.occupant.GetComponent<Pickup>()){
					AddItemToAIInventory(chosenSpace.occupant.GetComponent<Pickup>().id, enemyUC);
					InformationManager.im.AddToCombatLog("\n" + enemy.name + " Gains <color=green>" + ItemsList.AllItemInfo[chosenSpace.occupant.GetComponent<Pickup>().id].item_name + "</color>");

					if(ItemsList.AllItemInfo[chosenSpace.occupant.GetComponent<Pickup>().id].equippable ){
						SwapHeldWeapon(chosenSpace.occupant.GetComponent<Pickup>().id, enemyUC);
					}
					Destroy(temp);
				}

				//move to the space
				//let the board and the unit know which space is being occupied and play walk animation
				if(chosenSpace != enemyUC.occupiedSpace){
					//slightly redundant path find
					List<MapHexTile> path = PathfindingHandler.FindPath(enemyUC.occupiedSpace, chosenSpace.GetComponent<MapHexTile>());
					path.Reverse();

					//occupy
					enemy.GetComponent<UnitController>().occupiedSpace.occupant = null;
					enemy.GetComponent<UnitController>().occupiedSpace = chosenSpace.GetComponent<MapHexTile>();
					chosenSpace.GetComponent<MapHexTile>().occupant = enemy;
				
					//walk the path to the chosen space
					//enemyUC.spacesWalked = Mathf.RoundToInt(enemyUC.MovementRange);
					enemyUC.spacesWalked += path.Count -1;
					yield return enemyUC.StartCoroutine(enemyUC.WalkPath(path, BattleController.bc.showNonPlayerTurns));
					//destroy the physical item on the ground after walking
					
					enemyActionsPointsSpent += 1;
					
				}

				if(showAIMovement) yield return new WaitForSeconds (0.3f);
				
		}

		//adding this because the recursion caused crashes
		yield return new WaitForEndOfFrame();

		if(objectiveIsItem && enemyUC.spacesWalked < enemyUC.MovementRange){
			yield return enemyUC.StartCoroutine(Move(enemyUC, enemy));
		}
	}

    public GameObject NearestHostileEnemy(GameObject caster, bool canPickUpItems){
		
		GameObject selected = null;
		float nearestDistance = 2000;
		//Items
		if(canPickUpItems){
			if(caster.GetComponent<UnitController>().aiInfo.bagItems.Count < 3){
				foreach (Pickup target in FindObjectsOfType<Pickup>())
				{
					if(target != null){
						
						float distance = Vector3.Distance(caster.transform.position, target.transform.position);
						if(distance < nearestDistance ){
							nearestDistance = distance;
							selected = target.gameObject;
						}
					}
				}
			}
		}

        //Wild Monsters
		foreach (GameObject target in BattleController.bc.allEnemies)
		{
			if(target != null && target.tag != caster.tag){
				
                float distance = Vector3.Distance(caster.transform.position, target.transform.position);
				if(distance < nearestDistance ){
					nearestDistance = distance;
					selected = target;
				}
			}
		}
        foreach (GameObject target in BattleController.bc.team1Players)
		{
			if(target != null && target.tag != caster.tag && target.GetComponent<Health>().currentHealth > 0){
				
                float distance = Vector3.Distance(caster.transform.position, target.transform.position);
				if(distance < nearestDistance ){
					nearestDistance = distance;
					selected = target;
				}
			}
		}
        foreach (GameObject target in BattleController.bc.team2Players)
		{
			if(target != null && target.tag != caster.tag && target.GetComponent<Health>().currentHealth > 0){
				
                float distance = Vector3.Distance(caster.transform.position, target.transform.position);
				if(distance < nearestDistance ){
					nearestDistance = distance;
					selected = target;
				}
			}
		}
		//check local player as a target
		if(BattleController.bc.player != null && BattleController.bc.player.tag != caster.tag && BattleController.bc.player.GetComponent<Health>().currentHealth > 0){
				
			float distance = Vector3.Distance(caster.transform.position, BattleController.bc.player.transform.position);
			if(distance < nearestDistance ){
				nearestDistance = distance;
				selected = BattleController.bc.player;
			}
		}
		//if nothing is selected, select the current occupied tile to stay still
		if(selected == null){
			selected = caster.GetComponent<UnitController>().occupiedSpace.gameObject;
		}
		Debug.Log(caster.name + ": Distance to target: " + nearestDistance + "Target: " + selected.gameObject.name);
		return selected;
	}
	
	

	//swap weapons upon picking up a weapon if it is better than what we have
	void SwapHeldWeapon(int newItemID, UnitController wielderController){
		//determine which weapon is better by quantifying its stats and effects
		Item newItem = ItemsList.AllItemInfo[newItemID];
		float totalNewStats = newItem.item_damage_roll_count * newItem.item_damage_roll_sides * (1+ newItem.item_crit_chance*0.01f );
		totalNewStats += newItem.item_range;
		if(newItem.item_passives != null){
			foreach(string passive in newItem.item_passives){totalNewStats += 2;}
		}
		//force cursed items to stay equipped
		if(newItem.cursed == true){
			totalNewStats += 1000;
		}

		Item oldItem = wielderController.aiInfo.handItems[0];
		float totaloldStats = oldItem.item_damage_roll_count * oldItem.item_damage_roll_sides * (1+ oldItem.item_crit_chance*0.01f );
		totaloldStats += oldItem.item_range;
		if(oldItem.item_passives != null){
			foreach(string passive in oldItem.item_passives){totaloldStats += 2;}
		}
		if(oldItem.cursed == true){
			totaloldStats += 1000;
		}

		//Swap 2 Items 
		if(totalNewStats > totaloldStats){
				wielderController.aiInfo.bagItems.Remove(newItem);
				wielderController.aiInfo.bagItems.Add(oldItem);
				wielderController.aiInfo.handItems.Remove(oldItem);
				wielderController.aiInfo.handItems.Add(newItem);
				inventory.ApplyItemEffects(newItem, wielderController.gameObject);
				inventory.RemoveItemEffects(oldItem, wielderController.gameObject);
				inventory.AssignWeaponStats(wielderController.gameObject, newItemID);

		}

	}

}

[System.Serializable]
public class AIPlayerInfo{
    public int team;//1or2
    public int jobID;
    public List<Item> bagItems = new List<Item>(); 
	public List<Item> handItems = new List<Item>(); 
}
