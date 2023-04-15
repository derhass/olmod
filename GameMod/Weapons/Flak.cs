﻿using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class Flak : Weapon
    {
        FieldInfo flak_fire_count_Field = AccessTools.Field(typeof(PlayerShip), "flak_fire_count");

        public Flak(Ship s)
        {
            ship = s;

            displayName = "FLAK";
            UsesAmmo = true;
        }

        public override void Fire(Player player, float refire_multiplier)
        {
            int flak_fire_count = (int)flak_fire_count_Field.GetValue(player.c_player_ship);
            Vector3 c_right = (Vector3)c_right_Field.GetValue(player.c_player_ship);
            Vector3 c_up = (Vector3)c_up_Field.GetValue(player.c_player_ship);

            player.c_player_ship.FiringVolumeModifier = 0.75f;
            ProjPrefab type = ProjPrefab.proj_flak_cannon;
            Quaternion localRotation;
            if (player.m_weapon_level[(int)player.m_weapon_type] == WeaponUnlock.LEVEL_2B)
            {
                if (flak_fire_count == 0)
                {
                    GameManager.m_audio.PlayCue2D(337, 0.7f, 0.5f, 0f, true);
                    GameManager.m_audio.PlayCue2D(338, 0.7f, 0.5f, 0f, true);
                    flak_fire_count++;
                    player.c_player_ship.m_refire_time = 0.15f;
                    flak_fire_count_Field.SetValue(player.c_player_ship, flak_fire_count);
                    return;
                }
                float angle = 5f + (float)Mathf.Min(7, flak_fire_count) * 0.2f;
                localRotation = AngleRandomize(player.c_player_ship.c_transform_rotation, angle, c_up, c_right);
                // originally ProjectileManager.PlayerFire()
                MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_right.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], true, 0);
                localRotation = AngleRandomize(player.c_player_ship.c_transform_rotation, angle, c_up, c_right);
                MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_left.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 1);
                player.c_player_ship.m_refire_time += 0.08f + (float)Mathf.Max(0, 4 - flak_fire_count) * 0.01f;
                if (!GameplayManager.IsMultiplayer)
                {
                    player.c_player_ship.c_rigidbody.AddForce(UnityEngine.Random.onUnitSphere * (UnityEngine.Random.Range(20f, 30f) * player.c_player_ship.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
                    player.c_player_ship.c_rigidbody.AddTorque(UnityEngine.Random.onUnitSphere * (UnityEngine.Random.Range(2f, 3f) * player.c_player_ship.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
                }
                player.PlayCameraShake(CameraShakeType.FIRE_FLAK, 0.9f, 1.1f);
                flak_fire_count++;
                player.UseAmmo(1);
                flak_fire_count_Field.SetValue(player.c_player_ship, flak_fire_count);
                return;
            }
            float angle2 = ((!GameplayManager.IsMultiplayerActive) ? 8f : 6f);
            if (player.c_player_ship.m_alternating_fire)
            {
                localRotation = AngleRandomize(player.c_player_ship.c_transform.localRotation, angle2, c_up, c_right);
                MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_right.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], true, 0);
                player.c_player_ship.m_refire_time += ((player.m_weapon_level[(int)player.m_weapon_type] < WeaponUnlock.LEVEL_1) ? 0.095f : 0.075f);
                return;
            }
            localRotation = AngleRandomize(player.c_player_ship.c_transform.localRotation, angle2, c_up, c_right);
            MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_left.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 1);
            player.c_player_ship.m_refire_time += 0.03f;
            if (!GameplayManager.IsMultiplayer)
            {
                player.c_player_ship.c_rigidbody.AddForce(UnityEngine.Random.onUnitSphere * (UnityEngine.Random.Range(10f, 20f) * player.c_player_ship.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
                player.c_player_ship.c_rigidbody.AddTorque(UnityEngine.Random.onUnitSphere * (UnityEngine.Random.Range(1f, 2f) * player.c_player_ship.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
            }
            player.PlayCameraShake(CameraShakeType.FIRE_FLAK, 0.6f, 1f);
            player.UseAmmo(1);
        }
    }
}