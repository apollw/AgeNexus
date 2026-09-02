#!/usr/bin/env python3
"""Produces a conservative, canonical JSON summary from an AoE II recorded game."""

import importlib.metadata
import json
import sys

from mgz.summary import Summary


def value(data, *path):
    current = data
    for part in path:
        if current is None or part not in current:
            return None
        current = current[part]
    if current is None:
        return None
    if hasattr(current, "total_seconds"):
        return int(current.total_seconds())
    if isinstance(current, bool):
        return int(current)
    if isinstance(current, (int, float)):
        return int(round(current))
    return current


def main(path):
    with open(path, "rb") as replay:
        summary = Summary(replay)
        players = summary.get_players()

    output_players = []
    available_core = 0
    for player in players:
        achievements = player.get("achievements", {})
        military = achievements.get("military", {})
        economy = achievements.get("economy", {})
        technology = achievements.get("technology", {})
        society = achievements.get("society", {})
        row = {
            "name": player.get("name", ""),
            "isHuman": bool(player.get("human")),
            "teamNumber": int(player.get("team_id", -1)),
            "values": {
                "unitsKilled": value(military, "units_killed"),
                "unitsLost": value(military, "units_lost"),
                "buildingsDestroyed": value(military, "buildings_razed"),
                "buildingsLost": value(military, "buildings_lost"),
                "largestArmy": None,
                "peakVillagers": value(society, "villager_high"),
                "foodCollected": value(economy, "food_collected"),
                "woodCollected": value(economy, "wood_collected"),
                "goldCollected": value(economy, "gold_collected"),
                "stoneCollected": value(economy, "stone_collected"),
                "militaryScore": value(military, "score"),
                "economyScore": value(economy, "score"),
                "technologyScore": value(technology, "score"),
                "societyScore": value(society, "score"),
                "totalScore": value(player, "score"),
                "unitsConverted": value(military, "units_converted"),
                "tradeGold": value(economy, "trade_gold"),
                "relicGold": value(economy, "relic_gold"),
                "tributeSent": value(economy, "tribute_sent"),
                "tributeReceived": value(economy, "tribute_received"),
                "researchCount": value(technology, "research_count"),
                "exploredPercent": value(technology, "explored_percent"),
                "feudalAgeSeconds": value(technology, "feudal_time"),
                "castleAgeSeconds": value(technology, "castle_time"),
                "imperialAgeSeconds": value(technology, "imperial_time"),
                "effectiveActionsPerMinute": value(player, "eapm")
            }
        }
        available_core += sum(1 for field in (
            "unitsKilled", "unitsLost", "buildingsDestroyed", "buildingsLost", "largestArmy",
            "peakVillagers", "foodCollected", "woodCollected", "goldCollected", "stoneCollected",
            "militaryScore", "economyScore", "technologyScore", "societyScore", "totalScore"
        ) if row["values"][field] is not None)
        output_players.append(row)

    parser_version = importlib.metadata.version("mgz")
    possible_core = len(output_players) * 15
    warnings = []
    if available_core < possible_core:
        warnings.append("O replay não contém todos os resultados finais; complete os campos ausentes manualmente.")
    print(json.dumps({
        "succeeded": True,
        "extractorVersion": f"aoc-mgz/{parser_version}",
        "coverageDetails": json.dumps({
            "coreFieldsAvailable": available_core,
            "coreFieldsExpected": possible_core,
            "requiresManualCompletion": available_core < possible_core
        }),
        "players": output_players,
        "warnings": warnings
    }, ensure_ascii=False))


if __name__ == "__main__":
    try:
        main(sys.argv[1])
    except Exception as exception:  # the host maps this to a safe error code
        print(json.dumps({
            "succeeded": False,
            "extractorVersion": None,
            "coverageDetails": "{}",
            "players": [],
            "errorCode": "ReplayParseFailed",
            "warnings": [str(exception)]
        }, ensure_ascii=False))
        sys.exit(2)
