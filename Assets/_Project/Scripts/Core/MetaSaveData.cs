using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 판 사이에 유지되는 메타 창고 (extraction 규칙의 저장소).
    /// 생환 시: 전리품은 골드로 환산해 적립, 남은 소모품은 여기 보관된다.
    /// 허브에서 출정 백팩으로 고른 물품만 새 판에 반입하며, 죽으면 그 판 소지품은 소실된다.
    /// </summary>
    /// <summary>
    /// 해금 조건 하나의 최고 기록. <c>JsonUtility</c>가 직렬화할 수 있게 클래스로 둔다
    /// (구조체 목록은 Unity JSON 직렬화에서 다루기 번거롭다).
    /// </summary>
    [Serializable]
    public class UnlockProgressEntry
    {
        public int kind;
        public int best;
    }

    [Serializable]
    public class MetaSaveData
    {
        public int gold;
        public string[] unlockedHeroes = { "knight" };
        /// <summary>
        /// 창고와 출정 로드아웃. 아이템 종류마다 필드를 늘리지 않도록 목록 하나로 둔다
        /// (연산은 <see cref="ItemStorage"/>가 공유). 전리품은 창고에 남기지 않는다 —
        /// 생환 시 항상 골드로 환산되므로 <see cref="AddCount"/>가 걸러낸다.
        /// </summary>
        public List<ItemStack> stash = new List<ItemStack>();
        public List<ItemStack> loadout = new List<ItemStack>();

        // 장착 중인 장비 id (EquipmentCatalog). 빈 문자열이면 맨손이다.
        // 장착 장비는 백팩 공간을 쓰지 않지만 **안전하지는 않다** — 원정에 반입되며(창고에서 빠짐)
        // 죽으면 소모품과 함께 잃는다. 창고에 남긴 예비 장비만 안전하다(익스트랙션 규칙).
        public string equippedWeaponId = "";
        public string equippedGearId = "";

        // 현재 원정에 걸린 의뢰 id 목록. 생환/승리 정산 때 비워지고 허브에서 다시 채운다.
        public string[] activeBountyIds = new string[0];

        /// <summary>
        /// 조건을 달성해 드랍 풀에 들어온 도구들((int)<see cref="ItemKind"/>).
        /// <b>죽어도 남는다</b> — 실패한 판도 전진이어야 한다(<see cref="ItemUnlockRules"/>).
        /// 옛 세이브는 빈 목록으로 들어와 "아직 아무것도 안 열림"이 되므로 마이그레이션이 없다.
        /// </summary>
        public List<int> unlockedItems = new List<int>();

        /// <summary>
        /// 해금 조건별 <b>최고 기록</b>. 기록실이 "얼마나 가까웠나"를 보여주는 데 쓴다.
        /// <para>
        /// 지난 판 값이 아니라 최고 기록인 이유: 조건은 한 판 기준이라 나쁜 판 뒤에 0으로
        /// 돌아가면 안내가 쓸모없어진다. 최고 기록은 단조 증가해서 "8/12 까지 갔었다"가 남는다.
        /// 텔레메트리 리포트는 개발 빌드에서만 저장되므로 여기 담아야 배포 빌드에서도 보인다.
        /// </para>
        /// </summary>
        public List<UnlockProgressEntry> unlockProgress = new List<UnlockProgressEntry>();

        /// <summary>
        /// 던전에서 구출해 쉘터에 합류한 동료들(<see cref="ShelterNpcRoster"/>).
        /// 이들이 시설을 연다 — 미구출 시설은 허브에 프롭도 상호작용도 없다.
        /// <b>죽어도 남는다</b>: 구출은 소지품이 아니라 진행이다.
        /// </summary>
        public string[] rescuedNpcs = new string[0];

        /// <summary>
        /// 쓰지 않고 남은 <b>기록</b>. 판이 끝날 때마다 쌓이고 기록실에서 해금에 투입한다
        /// (<see cref="RunRecordRules"/>). 죽음이 먹이는 유일한 축이라 실패한 판도 여기서 전진한다.
        /// </summary>
        public int records;

        /// <summary>
        /// 역대 최대 도달 층 수. <b>개척 보너스의 기준선</b>이라 여기 저장해야 한다 —
        /// 이 값이 없으면 1~3층 왕복이 최적 파밍이 된다.
        /// </summary>
        public int deepestFloorsEver;

        /// <summary>
        /// 조건별로 <b>투입한</b> 기록. 최고 기록(<see cref="unlockProgress"/>)에 더해져
        /// 목표를 넘기면 해금된다. 최고 기록과 따로 두는 이유는 둘의 출처가 다르기 때문이다 —
        /// 하나는 플레이로 달성한 값이고 하나는 플레이어가 산 값이다.
        /// </summary>
        public List<UnlockProgressEntry> unlockInvested = new List<UnlockProgressEntry>();

        public int GetCount(ItemKind kind) => ItemStorage.Count(stash, kind);

        /// <summary>
        /// 창고 수량을 더한다. 전리품(<see cref="ItemCategory.Treasure"/>)은 보관하지 않는다 —
        /// 생환 정산에서 항상 골드로 바뀌므로 창고에 남으면 이중 계산이 된다.
        /// </summary>
        public void AddCount(ItemKind kind, int amount)
        {
            if (ItemCatalog.CategoryOf(kind) == ItemCategory.Treasure) return;
            ItemStorage.Add(stash, kind, amount);
        }

        /// <summary>창고에서 요청 수량만 제거한다. 보유량을 넘는 요청은 실제 제거량만 반환한다.</summary>
        public int RemoveCount(ItemKind kind, int amount) =>
            ItemStorage.Remove(stash, kind, amount);

        public int GetLoadoutCount(ItemKind kind) => ItemStorage.Count(loadout, kind);

        public void AddLoadoutCount(ItemKind kind, int amount) =>
            ItemStorage.Add(loadout, kind, amount);

        public int RemoveLoadoutCount(ItemKind kind, int amount) =>
            ItemStorage.Remove(loadout, kind, amount);

        public void ClearLoadout() => ItemStorage.Clear(loadout);

        public void ClearItems()
        {
            ItemStorage.Clear(stash);
            ClearLoadout();
        }

        /// <summary>장비를 하나라도 보유하고 있는가(제작 후 창고에 남아 있는 것).</summary>
        public bool OwnsEquipment(EquipmentDefinition definition) =>
            definition != null && GetCount(definition.Item) > 0;

        /// <summary>슬롯에 장착된 장비 id. 없으면 빈 문자열.</summary>
        public string GetEquipped(EquipmentSlot slot) =>
            slot == EquipmentSlot.Weapon ? equippedWeaponId ?? "" : equippedGearId ?? "";

        public void SetEquipped(EquipmentSlot slot, string equipmentId)
        {
            string value = equipmentId ?? "";
            if (slot == EquipmentSlot.Weapon) equippedWeaponId = value;
            else equippedGearId = value;
        }

        /// <summary>현재 장착 조합의 전투 보정. 보유하지 않은 장비는 장착으로 치지 않는다.</summary>
        public CombatLoadout EquippedLoadout()
        {
            EquipmentDefinition weapon = EquipmentCatalog.ById(equippedWeaponId);
            EquipmentDefinition gear = EquipmentCatalog.ById(equippedGearId);
            return EquipmentRules.LoadoutFor(
                OwnsEquipment(weapon) ? weapon.Id : null,
                OwnsEquipment(gear) ? gear.Id : null);
        }

        /// <summary>골드가 충분하면 차감하고 true. 상점 구매/해금 공통 경로.</summary>
        public bool TrySpend(int cost)
        {
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
            if (gold < cost) return false;
            gold -= cost;
            return true;
        }

        public bool IsHeroUnlocked(string heroId)
        {
            if (unlockedHeroes == null) return false;
            foreach (string id in unlockedHeroes)
                if (id == heroId) return true;
            return false;
        }

        public void UnlockHero(string heroId)
        {
            if (IsHeroUnlocked(heroId)) return;
            var next = new string[(unlockedHeroes?.Length ?? 0) + 1];
            unlockedHeroes?.CopyTo(next, 0);
            next[next.Length - 1] = heroId;
            unlockedHeroes = next;
        }

        /// <summary>이 조건의 최고 기록. 없으면 0.</summary>
        public int BestUnlockProgress(ItemKind kind)
        {
            if (unlockProgress == null) return 0;
            foreach (UnlockProgressEntry entry in unlockProgress)
                if (entry.kind == (int)kind) return entry.best;
            return 0;
        }

        /// <summary>최고 기록을 갱신한다(줄어들지 않는다).</summary>
        public void RecordUnlockProgress(ItemKind kind, int value)
        {
            if (value <= 0) return;
            unlockProgress ??= new List<UnlockProgressEntry>();

            foreach (UnlockProgressEntry entry in unlockProgress)
            {
                if (entry.kind != (int)kind) continue;
                if (value > entry.best) entry.best = value;
                return;
            }

            unlockProgress.Add(new UnlockProgressEntry { kind = (int)kind, best = value });
        }

        /// <summary>이 조건에 투입한 기록. 없으면 0.</summary>
        public int InvestedRecords(ItemKind kind)
        {
            if (unlockInvested == null) return 0;
            foreach (UnlockProgressEntry entry in unlockInvested)
                if (entry.kind == (int)kind) return entry.best;
            return 0;
        }

        /// <summary>
        /// 남은 기록에서 <paramref name="amount"/>만큼 이 조건에 투입한다.
        /// 보유량을 넘으면 <b>가진 만큼만</b> 넣고 실제 투입량을 돌려준다 —
        /// 실패시키는 대신 부분 투입을 허용해야 "조금씩 메운다"가 성립한다.
        /// </summary>
        public int InvestRecords(ItemKind kind, int amount)
        {
            int spend = Math.Min(Math.Max(0, amount), Math.Max(0, records));
            if (spend <= 0) return 0;

            records -= spend;
            unlockInvested ??= new List<UnlockProgressEntry>();
            foreach (UnlockProgressEntry entry in unlockInvested)
            {
                if (entry.kind != (int)kind) continue;
                entry.best += spend;
                return spend;
            }

            unlockInvested.Add(new UnlockProgressEntry { kind = (int)kind, best = spend });
            return spend;
        }

        /// <summary>
        /// 판이 끝날 때 기록을 적립하고 개척 기준선을 올린다. 적립량을 돌려준다.
        /// <b>순서가 중요하다</b> — 기준선을 먼저 올리면 개척 보너스가 사라진다.
        /// </summary>
        public int AwardRecords(int reachedFloors, int secretRoomsFound)
        {
            int gained = RunRecordRules.Award(reachedFloors, deepestFloorsEver, secretRoomsFound);
            records += gained;
            if (reachedFloors > deepestFloorsEver) deepestFloorsEver = reachedFloors;
            return gained;
        }

        public bool IsNpcRescued(string npcId)
        {
            if (rescuedNpcs == null || string.IsNullOrEmpty(npcId)) return false;
            foreach (string id in rescuedNpcs)
                if (id == npcId) return true;
            return false;
        }

        /// <summary>새로 구출했으면 true. 이미 합류했으면 아무것도 하지 않는다.</summary>
        public bool RescueNpc(string npcId)
        {
            if (string.IsNullOrEmpty(npcId) || IsNpcRescued(npcId)) return false;

            var next = new string[(rescuedNpcs?.Length ?? 0) + 1];
            rescuedNpcs?.CopyTo(next, 0);
            next[next.Length - 1] = npcId;
            rescuedNpcs = next;
            return true;
        }

        /// <summary>이 시설이 지금 쉘터에 있는가.</summary>
        public bool IsFacilityOpen(ShelterFacility facility) =>
            ShelterNpcRoster.IsFacilityOpen(facility, rescuedNpcs ?? new string[0]);

        public bool IsItemUnlocked(ItemKind kind)
        {
            if (unlockedItems == null) return false;
            foreach (int id in unlockedItems)
                if (id == (int)kind) return true;
            return false;
        }

        /// <summary>새로 열렸으면 true. 이미 열려 있으면 아무것도 하지 않는다(중복 방지).</summary>
        public bool UnlockItem(ItemKind kind)
        {
            if (IsItemUnlocked(kind)) return false;
            unlockedItems ??= new List<int>();
            unlockedItems.Add((int)kind);
            return true;
        }

        /// <summary>해금된 종류 목록 — 생성기에 넘기는 형태.</summary>
        public List<ItemKind> UnlockedItemKinds()
        {
            var kinds = new List<ItemKind>();
            if (unlockedItems == null) return kinds;
            foreach (int id in unlockedItems) kinds.Add((ItemKind)id);
            return kinds;
        }

    }
}
