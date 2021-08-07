using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Spells.Spellweaving
{
    public class GiftOfLifeSpell : ArcanistSpell, ISpellTargetingMobile
    {
        private static readonly SpellInfo m_Info = new(
            "Gift of Life",
            "Illorae",
            -1
        );

        private static readonly Dictionary<Mobile, ExpireTimer> m_Table = new();

        public GiftOfLifeSpell(Mobile caster, Item scroll = null)
            : base(caster, scroll, m_Info)
        {
        }

        public override TimeSpan CastDelayBase => TimeSpan.FromSeconds(4.0);

        public override double RequiredSkill => 38.0;
        public override int RequiredMana => 70;

        public double HitsScalar => (Caster.Skills.Spellweaving.Value / 2.4 + FocusLevel) / 100;

        public void Target(Mobile m)
        {
            if (m == null)
            {
                Caster.SendLocalizedMessage(1072077); // You may only cast this spell on yourself or a bonded pet.
            }
            else if (!Caster.CanSee(m))
            {
                Caster.SendLocalizedMessage(500237); // Target can not be seen.
            }
            else if (m.IsDeadBondedPet || !m.Alive)
            {
                // As per Osi: Nothing happens.
            }
            else if (m != Caster && !(m is BaseCreature bc && bc.IsBonded && bc.ControlMaster == Caster))
            {
                Caster.SendLocalizedMessage(1072077); // You may only cast this spell on yourself or a bonded pet.
            }
            else if (m_Table.ContainsKey(m))
            {
                Caster.SendLocalizedMessage(501775); // This spell is already in effect.
            }
            else if (CheckBSequence(m))
            {
                if (Caster == m)
                {
                    Caster.SendLocalizedMessage(1074774); // You weave powerful magic, protecting yourself from death.
                }
                else
                {
                    Caster.SendLocalizedMessage(1074775); // You weave powerful magic, protecting your pet from death.
                    SpellHelper.Turn(Caster, m);
                }

                m.PlaySound(0x244);
                m.FixedParticles(0x3709, 1, 30, 0x26ED, 5, 2, EffectLayer.Waist);
                m.FixedParticles(0x376A, 1, 30, 0x251E, 5, 3, EffectLayer.Waist);

                var skill = Caster.Skills.Spellweaving.Value;

                var duration = TimeSpan.FromMinutes((int)(skill / 24) * 2 + FocusLevel);

                var t = new ExpireTimer(m, duration, this);
                t.Start();

                m_Table[m] = t;

                BuffInfo.AddBuff(m, new BuffInfo(BuffIcon.GiftOfLife, 1031615, 1075807, duration, m, null, true));
            }

            FinishSequence();
        }

        public static void Initialize()
        {
            EventSink.PlayerDeath += HandleDeath;
        }

        public override void OnCast()
        {
            Caster.Target = new SpellTargetMobile(this, TargetFlags.Beneficial, 10);
        }

        public static void HandleDeath(Mobile m)
        {
            if (m_Table.ContainsKey(m))
            {
                Timer.StartTimer(TimeSpan.FromSeconds(Utility.RandomMinMax(2, 4)), () => HandleDeath_OnCallback(m));
            }
        }

        private static void HandleDeath_OnCallback(Mobile m)
        {
            if (!m_Table.TryGetValue(m, out var timer))
            {
                return;
            }

            var hitsScalar = timer.Spell.HitsScalar;

            if (m is BaseCreature pet && pet.IsDeadBondedPet)
            {
                var master = pet.GetMaster();

                if (master?.NetState != null && Utility.InUpdateRange(pet, master))
                {
                    master.CloseGump<PetResurrectGump>();
                    master.SendGump(new PetResurrectGump(master, pet, hitsScalar));
                }
                else
                {
                    var friends = pet.Friends;

                    for (var i = 0; i < friends?.Count; i++)
                    {
                        var friend = friends[i];

                        if (friend.NetState != null && Utility.InUpdateRange(pet, friend))
                        {
                            friend.CloseGump<PetResurrectGump>();
                            friend.SendGump(new PetResurrectGump(friend, pet));
                            break;
                        }
                    }
                }
            }
            else
            {
                m.CloseGump<ResurrectGump>();
                m.SendGump(new ResurrectGump(m, hitsScalar));
            }

            // Per OSI, buff is removed when gump sent, irregardless of online status or acceptance
            timer.DoExpire();
        }

        public static void OnLogin(Mobile m)
        {
            if (m?.Alive != false || m_Table[m] == null)
            {
                return;
            }

            HandleDeath_OnCallback(m);
        }

        private class ExpireTimer : Timer
        {
            private readonly Mobile m_Mobile;

            public ExpireTimer(Mobile m, TimeSpan delay, GiftOfLifeSpell spell)
                : base(delay)
            {
                m_Mobile = m;
                Spell = spell;
            }

            public GiftOfLifeSpell Spell { get; }

            protected override void OnTick()
            {
                DoExpire();
            }

            public void DoExpire()
            {
                Stop();

                m_Mobile.SendLocalizedMessage(1074776); // You are no longer protected with Gift of Life.
                m_Table.Remove(m_Mobile);

                BuffInfo.RemoveBuff(m_Mobile, BuffIcon.GiftOfLife);
            }
        }
    }
}
