﻿using System;
using System.Collections;
using System.Collections.Generic;
using Amplitude;
using Amplitude.Mercury;
using Amplitude.Mercury.Data.Simulation;
using Amplitude.Mercury.Interop;
using Amplitude.Mercury.Sandbox;
using Amplitude.Mercury.Simulation;
using Amplitude.Mercury.WorldGenerator;
using Amplitude.Serialization;
using BepInEx.Configuration;
using FailureFlags = Amplitude.Mercury.Simulation.FailureFlags;

namespace Gedemon.TrueCultureLocation
{
    class CultureChange
    {
		public static MinorEmpire DoLiberateSettlement(Settlement settlement, MajorEmpire majorEmpire)
		{
			MinorEmpire minorEmpire = Amplitude.Mercury.Sandbox.Sandbox.MinorFactionManager.PeacefulLiberateHumanSpawner.GetOrAllocateMinorEmpireFor(settlement);
			if (minorEmpire.RankedMajorEmpireIndexes[0] != majorEmpire.Index)
			{
				int num = Array.IndexOf(minorEmpire.RankedMajorEmpireIndexes, majorEmpire.Index);
				minorEmpire.RankedMajorEmpireIndexes[num] = minorEmpire.RankedMajorEmpireIndexes[0];
				minorEmpire.RankedMajorEmpireIndexes[0] = majorEmpire.Index;
			}
			List<int> territoryIndices = settlement.Region.Entity.TerritoryIndices;
			for (int num2 = majorEmpire.Squadrons.Count - 1; num2 >= 0; num2--)
			{
				Squadron squadron = majorEmpire.Squadrons[num2];
				if (squadron.AircraftCarrierArmy.Entity == null && territoryIndices.Contains(squadron.TerritoryIndex))
				{
					DepartmentOfDefense.ReleaseSquadron(squadron);
				}
			}

			DepartmentOfTheInterior.ChangeSettlementOwner(settlement, minorEmpire, keepCaptured: false);
			BaseHumanSpawnerDefinition spawnerDefinitionForMinorEmpire = minorEmpire.Spawner.GetSpawnerDefinitionForMinorEmpire(minorEmpire);
			MinorToMajorRelation minorToMajorRelation = minorEmpire.RelationsToMajor[majorEmpire.Index];
			if (minorToMajorRelation.PatronageStock.Value < 50)
			{
				minorToMajorRelation.PatronageStock.Value = 50;
				MinorFactionManager.RefreshPatronageState(minorToMajorRelation, minorEmpire.PatronageDefinition);
			}
			FixedPoint defaultGameSpeedMultiplier = Amplitude.Mercury.Sandbox.Sandbox.GameSpeedController.CurrentGameSpeedDefinition.DefaultGameSpeedMultiplier;
			minorEmpire.RemainingLifeTime += (int)FixedPoint.Ceiling(spawnerDefinitionForMinorEmpire.AddedLifeTimeInTurnsForNewPatronnage * defaultGameSpeedMultiplier);
			Amplitude.Mercury.Sandbox.Sandbox.VisibilityController.DirtyEmpireVision(minorEmpire);

			return minorEmpire;
		}

		public static bool TryInitializeFreeMajorEmpireToReplace(MajorEmpire majorEmpire, out MajorEmpire oldEmpire)
		{
			Diagnostics.LogWarning($"[Gedemon] TryInitializeFreeMajorEmpireToReplace for {majorEmpire.FactionDefinition.Name}...");

			oldEmpire = null;
			MajorEmpire potentialLiege = majorEmpire.Liege.Entity != null ? majorEmpire.Liege.Entity : majorEmpire;

			Diagnostics.LogWarning($"[Gedemon] potential Liege of the old Empire : {potentialLiege.FactionDefinition.Name}");

			int numMajor = Amplitude.Mercury.Sandbox.Sandbox.MajorEmpires.Length;

			// check for unused Empire in the pool first
			bool mustResurect = true;
			for (int i = 0; i < numMajor; i++)
			{
				MajorEmpire potentialEmpire = Sandbox.MajorEmpires[i];

				if (potentialEmpire.Armies.Count == 0 && potentialEmpire.Settlements.Count == 0 && potentialEmpire.IsAlive)
                {
					mustResurect = false;
				}
			}

			for (int i = 0; i < numMajor; i++)
			{
				MajorEmpire potentialEmpire = Sandbox.MajorEmpires[i];

				Diagnostics.LogWarning($"[Gedemon] potentialEmpire = {potentialEmpire.FactionDefinition.Name}, Armies = {potentialEmpire.Armies.Count}, Settlements = {potentialEmpire.Settlements.Count}, mustResurect = {mustResurect}, isAlive = {potentialEmpire.IsAlive}, fame = {potentialEmpire.FameScore.Value}");

				// we don't want to resurect dead Empire with more fame than us !
				if (!potentialEmpire.IsAlive && potentialEmpire.FameScore.Value >= majorEmpire.FameScore.Value)
				{
					Diagnostics.LogWarning($"[Gedemon] aborting, potential resurrected vassal fame > current Empire fame ({majorEmpire.FameScore.Value})");
					continue;
                }

				if (potentialEmpire.Armies.Count == 0 && potentialEmpire.Settlements.Count == 0 && (potentialEmpire.IsAlive || mustResurect ))
				{

					oldEmpire = potentialEmpire;
					Diagnostics.LogWarning($"[Gedemon] DepartmentOfDevelopment.CurrentEraIndex: {oldEmpire.DepartmentOfDevelopment.CurrentEraIndex} => {majorEmpire.DepartmentOfDevelopment.CurrentEraIndex}");
					oldEmpire.IsAlive = true;
					oldEmpire.DepartmentOfDevelopment.nextFactionName = StaticString.Empty;
					oldEmpire.DepartmentOfDevelopment.isNextFactionConfirmed = false;
					Diagnostics.LogWarning($"[Gedemon] before changes, EraLevel: {oldEmpire.EraLevel.Value}");
					oldEmpire.DepartmentOfDevelopment.CurrentEraIndex = majorEmpire.DepartmentOfDevelopment.CurrentEraIndex;
					//oldEmpire.DepartmentOfScience.CurrentTechnologicalEraIndex = majorEmpire.DepartmentOfScience.CurrentTechnologicalEraIndex;
					oldEmpire.ChangeFaction(majorEmpire.FactionDefinition.Name); // before ApplyStartingEra !
					oldEmpire.DepartmentOfDevelopment.ApplyStartingEra();
					oldEmpire.DepartmentOfDevelopment.PickFirstEraStars();
					//oldEmpire.DepartmentOfDevelopment.ApplyNextEra();

					Diagnostics.LogWarning($"[Gedemon] DepartmentOfScience.CurrentTechnologicalEraIndex = {oldEmpire.DepartmentOfScience.CurrentTechnologicalEraIndex}");
					oldEmpire.DepartmentOfScience.UpdateCurrentTechnologyEraIfNeeded();
					Diagnostics.LogWarning($"[Gedemon] After UpdateCurrentTechnologyEraIfNeeded, CurrentTechnologicalEraIndex = {oldEmpire.DepartmentOfScience.CurrentTechnologicalEraIndex}");
					oldEmpire.DepartmentOfScience.CompleteAllPreviousErasTechnologiesOnStart();
					// Adding Techs
                    {
						int length = oldEmpire.DepartmentOfScience.Technologies.Length;
						for (int t = 0; t < length; t++)
						{
							ref Technology reference = ref oldEmpire.DepartmentOfScience.Technologies.Data[t];
							ref Technology reference2 = ref majorEmpire.DepartmentOfScience.Technologies.Data[t];

							//Diagnostics.LogWarning($"[Gedemon] Check {reference.TechnologyDefinition.Name} (replacing Empire = {reference.TechnologyState}, Major Empire {reference2.TechnologyDefinition.Name} = {reference2.TechnologyState})" );

							if (reference2.TechnologyState == TechnologyStates.Completed && reference.TechnologyState != TechnologyStates.Completed)
							{
								reference.InvestedResource = reference.Cost;
								reference.TechnologyState = TechnologyStates.Completed;
								oldEmpire.DepartmentOfScience.TechnologicalEras.Data[reference.EraIndex].ResearchedTechnologyCount++;
								oldEmpire.DepartmentOfScience.ApplyTechnologyEffects(reference.TechnologyDefinition, raiseSimulationEvents: false);
								Amplitude.Mercury.Sandbox.Sandbox.SimulationEntityRepository.SetSynchronizationDirty(oldEmpire);

								Diagnostics.LogWarning($"[Gedemon] Completing know Tech : {reference.TechnologyDefinition.Name} ({reference.TechnologyState})");
							}
						}

						oldEmpire.DepartmentOfScience.Technologies.Frame = Amplitude.Mercury.Sandbox.Sandbox.Frame;
						oldEmpire.DepartmentOfScience.TechnologicalEras.Frame = Amplitude.Mercury.Sandbox.Sandbox.Frame;
						oldEmpire.DepartmentOfScience.UpdateTechnologiesState();
					}

					Diagnostics.LogWarning($"[Gedemon] after changes, EraLevel: {oldEmpire.EraLevel.Value}");
					oldEmpire.SetEmpireSymbol(majorEmpire.EmpireSymbolDefinition.Name);
					// reset diplomatic relations
					for (int otherIndex = 0; otherIndex < numMajor; otherIndex++)
					{
						{
							DiplomaticRelation relationtoReset = Sandbox.DiplomaticAncillary.GetRelationFor(oldEmpire.Index, otherIndex);
							ResetDiplomaticAmbassy(relationtoReset.LeftAmbassy);
							ResetDiplomaticAmbassy(relationtoReset.RightAmbassy);
							relationtoReset.ApplyState(DiplomaticStateType.Unknown, otherIndex);
						}
					}
					// Set Vassal (to do : depending of stability)
					{
						DiplomaticRelation relationFor = Sandbox.DiplomaticAncillary.GetRelationFor(potentialLiege.Index, oldEmpire.Index);
						DiplomaticStateType state = relationFor.DiplomaticState.State;
						Diagnostics.LogWarning($"[Gedemon] Set Vassal from current DiplomaticState = {state}");
						/*
						if (state == DiplomaticStateType.VassalToLiege)
						{
							relationFor.ApplyState(DiplomaticStateType.War, majorEmpire.Index);
							SimulationEvent_DiplomaticStateChanged.Raise(majorEmpire, majorEmpire.Index, relationFor.DiplomaticState.State, state, oldEmpire.Index, -1);
							state = relationFor.DiplomaticState.State;
						}
						//*/
						relationFor.ApplyState(DiplomaticStateType.VassalToLiege, potentialLiege.Index);
						relationFor.UpdateAbilities(raiseSimulationEvents: true);
						SimulationEvent_DiplomaticStateChanged.Raise(potentialLiege, potentialLiege.Index, relationFor.DiplomaticState.State, state, oldEmpire.Index, -1);
						Diagnostics.LogWarning($"[Gedemon] New  DiplomaticState = {Sandbox.DiplomaticAncillary.GetRelationFor(potentialLiege.Index, oldEmpire.Index).DiplomaticState.State}");
						Sandbox.SimulationEntityRepository.SetSynchronizationDirty(potentialLiege);
						Sandbox.SimulationEntityRepository.SetSynchronizationDirty(oldEmpire);
					}
					break;
				}
			}
			return oldEmpire != null;
		}
		
		private static void ResetDiplomaticAmbassy(DiplomaticAmbassy diplomaticAmbassy)
		{
			diplomaticAmbassy.OnGoingDemands.Clear();
			diplomaticAmbassy.AvailableGrievances.Clear();
			diplomaticAmbassy.EmpireMoral.Empty();
		}

		public static bool GetTerritoryChangesOnEvolve(DepartmentOfDevelopment instance, StaticString nextFactionName, out int numCitiesLost, out District potentialCapital, ref IDictionary<Settlement, List<int>> territoryToDetachAndCreate, ref IDictionary<Settlement, List<int>> territoryToDetachAndFree, ref List<Settlement> settlementToLiberate, ref List<Settlement> settlementToFree)
		{

			Diagnostics.LogWarning($"[Gedemon] in GetTerritoryChangesOnEvolve.");

			potentialCapital = null;
			numCitiesLost = 0;

			MajorEmpire majorEmpire = instance.majorEmpire;
			//StaticString nextFactionName = instance.nextFactionName;

			Diagnostics.LogWarning($"[Gedemon] before IsEmpireHumanSlot");
			bool isHuman = TrueCultureLocation.IsEmpireHumanSlot(majorEmpire.Index);
			Diagnostics.LogWarning($"[Gedemon] before KeepOnlyCultureTerritory");
			bool keepOnlyCultureTerritory = TrueCultureLocation.KeepOnlyCultureTerritory();
			bool keepTerritoryAttached = TrueCultureLocation.KeepTerritoryAttached();
			bool capitalChanged = false; // the Capital hasn't been changed yet
			bool needNewCapital = false; // We need a new Capital (and we've not found it yet)

			if ((!isHuman) && TrueCultureLocation.NoTerritoryLossForAI())
			{
				keepOnlyCultureTerritory = false;
			}

			if (majorEmpire.DepartmentOfDevelopment.CurrentEraIndex != 0 && majorEmpire.FactionDefinition.Name != nextFactionName && keepOnlyCultureTerritory)
			{
				Diagnostics.LogWarning($"[Gedemon] before Check for new Capital (exist = {(majorEmpire.Capital.Entity != null)})");
				bool hasCapital = (majorEmpire.Capital.Entity != null);

				if (hasCapital)
				{
					Settlement Capital = majorEmpire.Capital;

					District capitalMainDistrict = Capital.GetMainDistrict();
					Diagnostics.LogWarning($"[Gedemon] before Check Capital Territory ({Capital.SettlementStatus} {Capital.EntityName} is current Capital)");
					needNewCapital = !CultureUnlock.HasTerritory(nextFactionName.ToString(), capitalMainDistrict.Territory.Entity.Index);

					if (needNewCapital && keepTerritoryAttached)
					{
						int count2 = Capital.Region.Entity.Territories.Count;
						for (int k = 0; k < count2; k++)
						{
							Territory territory = Capital.Region.Entity.Territories[k];
							if (CultureUnlock.HasTerritory(nextFactionName.ToString(), territory.Index))
							{
								needNewCapital = false;
								break;
							}
						}
					}

				}
				else
				{
					needNewCapital = true;
				}

				if (needNewCapital)
				{
					// need to find new Capital !
					Diagnostics.LogWarning($"[Gedemon] Searching new Capital in existing Cities first.");

					// check existing settlements first

					foreach (int territoryIndex in CultureUnlock.GetListTerritories(nextFactionName.ToString()))
					{
						int count4 = majorEmpire.Settlements.Count;
						for (int m = 0; m < count4; m++)
						{
							Settlement settlement = majorEmpire.Settlements[m];
							if (settlement.SettlementStatus == SettlementStatuses.City)
							{
								District potentialDistrict = settlement.GetMainDistrict();
								if (territoryIndex == potentialDistrict.Territory.Entity.Index)
								{
									Diagnostics.LogWarning($"[Gedemon] {settlement.SettlementStatus} {settlement.EntityName} : register City for new Capital in territory {potentialDistrict.Territory.Entity.Index}.");
									needNewCapital = false;
									capitalChanged = true;
									potentialCapital = potentialDistrict;
									break;
								}
							}
						}
						if (!needNewCapital)
                        {
							break;
                        }

					}


					if (needNewCapital)
					{
						Diagnostics.LogWarning($"[Gedemon] New Capital not found, now searching in Settlement without cities for a potential Capital position.");

						foreach (int territoryIndex in CultureUnlock.GetListTerritories(nextFactionName.ToString()))
						{

							int count5 = majorEmpire.Settlements.Count;
							for (int n = 0; n < count5; n++)
							{
								Settlement settlement = majorEmpire.Settlements[n];
								if (settlement.SettlementStatus != SettlementStatuses.City)
								{
									District potentialDistrict = settlement.GetMainDistrict();
									if (territoryIndex == potentialDistrict.Territory.Entity.Index)
									{
										Diagnostics.LogWarning($"[Gedemon] {settlement.SettlementStatus} {settlement.EntityName} : register Settlement to Create new Capital in territory {potentialDistrict.Territory.Entity.Index}.");
										needNewCapital = false;
										capitalChanged = true;
										potentialCapital = potentialDistrict;
										break;
									}
								}
							}
							if (!needNewCapital)
							{
								break;
							}
						}
					}
				}

				int count = majorEmpire.Settlements.Count;
				for (int j = 0; j < count; j++)
				{
					Settlement settlement = majorEmpire.Settlements[j];

					bool hasTerritoryFromNewCulture = false;

					int count2 = settlement.Region.Entity.Territories.Count;
					for (int k = 0; k < count2; k++)
					{
						Territory territory = settlement.Region.Entity.Territories[k];
						if (CultureUnlock.HasTerritory(nextFactionName.ToString(), territory.Index))
							hasTerritoryFromNewCulture = true;
					}

					//settlement.PublicOrderCurrent.Value

					bool keepSettlement = hasTerritoryFromNewCulture && keepTerritoryAttached;

					if (!keepSettlement)
					{
						if (settlement.SettlementStatus == SettlementStatuses.City)
						{
							District mainDistrict = settlement.GetMainDistrict();

							bool giveSettlement = !CultureUnlock.HasTerritory(nextFactionName.ToString(), mainDistrict.Territory.Entity.Index);

							Diagnostics.LogWarning($"[Gedemon] Settlement ID#{j}: City {settlement.EntityName} territories, give city = {giveSettlement}");

							for (int k = 0; k < count2; k++)
							{
								Territory territory = settlement.Region.Entity.Territories[(count2 - 1) - k]; // start from last to avoid "CannotDetachConnectorTerritory"
								if (territory.Index != mainDistrict.Territory.Entity.Index)
								{
									if (CultureUnlock.HasTerritory(nextFactionName.ToString(), territory.Index))
									{
										if (giveSettlement)
										{
											FailureFlags flag = majorEmpire.DepartmentOfTheInterior.CanDetachTerritoryFromCity(territory, settlement);
											if (flag == FailureFlags.None || flag == FailureFlags.CannotDetachConnectorTerritory)
											{
												Diagnostics.LogWarning($"[Gedemon] {settlement.SettlementStatus} {settlement.EntityName} : Add to territoryToDetachAndCreate for index = {territory.Index}, is in new Culture Territory but loosing City");

												if (territoryToDetachAndCreate.ContainsKey(settlement))
												{
													territoryToDetachAndCreate[settlement].Add(territory.Index);
												}
												else
												{
													territoryToDetachAndCreate.Add(settlement, new List<int> { territory.Index });
												}
											}
											else
											{
												Diagnostics.LogWarning($"[Gedemon] {settlement.SettlementStatus} {settlement.EntityName} : FAILED to detach and create for territory index = {territory.Index}, {majorEmpire.DepartmentOfTheInterior.CanDetachTerritoryFromCity(territory, settlement)}, is new Culture Territory but loosing city");
											}
										}
										else
										{
											Diagnostics.LogWarning($"[Gedemon] {settlement.SettlementStatus} {settlement.EntityName} : No change for territory index = {territory.Index}, is in new Culture Territory and keeping City");
										}
									}
									else
									{
										if (!giveSettlement)
										{
											FailureFlags flag = majorEmpire.DepartmentOfTheInterior.CanDetachTerritoryFromCity(territory, settlement);
											if (flag == FailureFlags.None || flag == FailureFlags.CannotDetachConnectorTerritory)
											{
												Diagnostics.LogWarning($"[Gedemon] {settlement.SettlementStatus} {settlement.EntityName} : Add to territoryToDetachAndFree for index = {territory.Index}, not in new Culture Territory and keeping city");

												if (territoryToDetachAndFree.ContainsKey(settlement))
												{
													territoryToDetachAndFree[settlement].Add(territory.Index);
												}
												else
												{
													territoryToDetachAndFree.Add(settlement, new List<int> { territory.Index });
												}
											}
											else
											{
												Diagnostics.LogWarning($"[Gedemon] {settlement.SettlementStatus} {settlement.EntityName} : FAILED to detach for territory index = {territory.Index}, {majorEmpire.DepartmentOfTheInterior.CanDetachTerritoryFromCity(territory, settlement)}, not in new Culture Territory and keeping city");
											}
										}
										else
										{
											Diagnostics.LogWarning($"[Gedemon] {settlement.SettlementStatus} {settlement.EntityName} : No change for territory index = {territory.Index}, not in new Culture Territory, as is city");
										}

									}
								}
							}
							if (giveSettlement)
							{
								numCitiesLost++;
								FailureFlags flag = DepartmentOfTheInterior.CanLiberateSettlement(majorEmpire, settlement);
								if (flag == FailureFlags.None || flag == FailureFlags.SettlementIsCapital)
								{
									Diagnostics.LogWarning($"[Gedemon] City {settlement.EntityName} : Add to settlementToLiberate");
									settlementToLiberate.Add(settlement);
								}
								else
								{
									Diagnostics.LogWarning($"[Gedemon] City {settlement.EntityName} : Can't Liberate ({flag}), Add to settlementToFree (after creating new Capital)");
									settlementToFree.Add(settlement);
								}
							}
						}
						else
						{
							Diagnostics.LogWarning($"[Gedemon] Settlement ID#{j}: Check {settlement.SettlementStatus} {settlement.EntityName} territory");

							if (settlement.SettlementStatus != SettlementStatuses.None)
							{
								Territory territory = settlement.Region.Entity.Territories[0];
								if (!CultureUnlock.HasTerritory(nextFactionName.ToString(), territory.Index))
								{
									Diagnostics.LogWarning($"[Gedemon] {settlement.SettlementStatus} {settlement.EntityName} : Add to settlementToFree for index = {territory.Index}, not city, not in new Culture Territory");
									settlementToFree.Add(settlement);
								}
								else
								{
									Diagnostics.LogWarning($"[Gedemon] {settlement.SettlementStatus} {settlement.EntityName} : No change for territory index = {territory.Index}, not city, is in new Culture Territory");
								}
							}
						}
					}
				}
			}

			return needNewCapital || capitalChanged;
		}
	}
}
