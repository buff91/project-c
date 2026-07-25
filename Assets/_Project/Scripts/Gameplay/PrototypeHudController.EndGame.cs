using System;
using ProjectC.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectC.Gameplay
{
    public partial class PrototypeHudController : MonoBehaviour
    {

        private void HandleExitChoiceRequested()
        {
            if (_exitModal == null || demo == null) return;
            CloseTransientOverlays();
            bool finalExit = !demo.HasNextStage;
            if (_exitTitle != null)
                _exitTitle.text = finalExit ? "봉인 해제된 출구" : "던전 출구";
            if (_exitDesc != null)
            {
                int gold = demo.CarriedTreasureGold();
                _exitDesc.text =
                    finalExit
                        ? $"{demo.BossName} 처치 완료 · 전리품 가치 " +
                          $"{ItemCatalog.FormatGold(gold)} · 정복을 확정할 수 있다"
                        : $"들고 있는 전리품 가치: {ItemCatalog.FormatGold(gold)} · " +
                          $"다음은 던전 {demo.StageIndex + 1}";
            }
            if (_exitAdvance != null)
                _exitAdvance.text = finalExit ? "던전 정복" : "더 깊이";
            _exitModal.BringToFront();
            _exitModal.AddToClassList("is-open");
        }

        private void HandleBossStateChanged() => UpdateBossPanel();

        private void UpdateBossPanel()
        {
            if (_bossPanel == null) return;

            bool show = demo != null && demo.IsBossFloor;
            _bossPanel.EnableInClassList("is-open", show);
            if (!show) return;

            if (_bossName != null)
                _bossName.text = demo.BossDefeated
                    ? $"{demo.BossName} · 처치 완료"
                    : demo.BossName;

            int maxHp = Mathf.Max(1, demo.BossMaxHp);
            int hp = Mathf.Clamp(demo.BossHp, 0, maxHp);
            if (_bossHealthFill != null)
                _bossHealthFill.style.width =
                    new Length(hp * 100f / maxHp, LengthUnit.Percent);
            if (_bossHealthValue != null)
                _bossHealthValue.text = demo.BossDefeated ? "EXIT UNSEALED" : $"{hp} / {maxHp}";
            if (_bossObjective != null)
                _bossObjective.text = demo.BossDefeated
                    ? "출구의 붉은 봉인이 청록빛으로 변했다 — 출구(▼)로 향하라"
                    : "보스를 쓰러뜨려 출구의 봉인을 해제하라";
        }

        private void HandleExitAdvance()
        {
            _exitModal?.RemoveFromClassList("is-open");
            demo?.ConfirmAdvanceStage();
        }

        private void HandleExitExtract()
        {
            _exitModal?.RemoveFromClassList("is-open");
            demo?.ExtractRun();
        }

        private void HandleRunEnded(RunSummary summary)
        {
            if (_gameoverOverlay == null) return;

            CloseTransientOverlays();

            bool survived = summary.Victory || summary.Extracted;
            _gameoverOverlay.EnableInClassList("is-victory", survived);
            if (_gameoverTitle != null)
                _gameoverTitle.text = summary.Victory ? "최심층 정복!"
                    : summary.Extracted ? "생환 성공!"
                    : "당신은 죽었습니다";
            if (_gameoverCause != null)
            {
                _gameoverCause.text = survived
                    ? (summary.GoldBanked > 0
                        ? $"+{ItemCatalog.FormatGold(summary.GoldBanked)} 창고 적립 · 소지품 보관 완료"
                        : "소지품을 창고에 보관했다")
                    : $"사인: {RunSummary.FormatCause(summary.CauseOfDeath)} — 소지품을 모두 잃었다";
                _gameoverCause.style.display = DisplayStyle.Flex;
            }
            if (_gameoverFloor != null)
                _gameoverFloor.text = $"도달 층: {IsoPrototypeDemo.FloorLabel(summary.DeepestFloorIndex)}";
            if (_gameoverKills != null)
                _gameoverKills.text = $"처치: {summary.Kills}";
            _gameoverOverlay.BringToFront();
            _gameoverOverlay.AddToClassList("is-open");
        }

        private void RestartRun()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void GoToMainMenu()
        {
            SceneManager.LoadScene(FrontEndFlow.MainMenuScene);
        }
    }
}
