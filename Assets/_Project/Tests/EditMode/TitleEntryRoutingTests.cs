using NUnit.Framework;
using ProjectC.Gameplay;

namespace ProjectC.Tests
{
    /// <summary>
    /// 타이틀 버튼 규칙. 사망 후 착지점이 캠프로 바뀐 뒤로 타이틀은 앱을 켤 때만 지나는
    /// 문이 됐다 — 그 전제가 깨지면(죽은 뒤 `이어하기`가 다시 뜨는) 여기서 잡는다.
    /// </summary>
    public class TitleEntryRoutingTests
    {
        [Test]
        public void Start_AlwaysLeadsToTheCamp()
        {
            // 첫 실행이든 재접속이든 같은 목적지다 — 버튼 이름과 목적지를 저장 상태로 흔들지 않는다.
            Assert.AreEqual(FrontEndFlow.HubScene, TitleEntryRouting.StartScene);
        }

        [Test]
        public void Resume_LeadsBackIntoTheDungeon()
        {
            Assert.AreEqual(FrontEndFlow.DungeonScene, TitleEntryRouting.ResumeScene);
        }

        [Test]
        public void Resume_ExistsOnlyWithAMidRunSave()
        {
            Assert.IsTrue(TitleEntryRouting.ShowsResume(hasRunSave: true));
            // 사망/생환 직후 상태다 — 체크포인트가 지워졌으니 버튼도 없다(회색 비활성이 아니라).
            Assert.IsFalse(TitleEntryRouting.ShowsResume(hasRunSave: false));
        }
    }
}
