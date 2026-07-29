using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ProjectC.Tests
{
    /// <summary>
    /// 타이포그래피 규칙을 USS 소스에서 직접 강제한다.
    ///
    /// 왜 테스트로 두나: 이 규칙들은 세 번의 오버라이드 시대(base / Torchstone v2 /
    /// Semantic icon pass)를 지나며 조용히 썩었다 — 문서에는 "9의 배수만"이라고 적혀 있는데
    /// 실제 선언 67개 중 50개가 위반이었고, 아무도 알아채지 못했다. 사람이 지키는 규칙이
    /// 아니라 빌드가 지키는 규칙으로 바꾼다.
    ///
    /// 세 규칙의 근거는 서로 다르다:
    /// - 9의 배수: Galmuri9은 9px 페이스다. 중간값은 위계를 만들지 못하고 종류만 늘린다.
    /// - 합성 볼드 금지: Regular 단일 페이스라 Unity가 래스터를 굵게 합성하며 획이 번진다.
    /// - 소수점 자간 금지: 어떤 배율에서도 도트 격자에 안 떨어진다.
    /// </summary>
    public sealed class UssTypographyTests
    {
        private static readonly int[] AllowedFontSizes = { 9, 18, 27, 36, 54 };

        private static string UiDirectory =>
            Path.Combine(TestProjectRoot(), "Assets", "_Project", "UI");

        private static string TestProjectRoot()
        {
            // 에디터에서는 Application.dataPath로 잡으면 되지만 dotnet shim에서도 돌아야
            // 하므로, 엔진 어셈블리에 의존하지 않고 소스 트리를 거슬러 올라가 찾는다.
            // (이 파일은 shim 제외 목록에 넣지 않는다 — 에디터 없이 도는 게 더 값지다.)
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Project", "UI")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException(
                "Assets/_Project/UI 를 찾지 못했다 (from "
                + TestContext.CurrentContext.TestDirectory + ")");
        }

        private static IEnumerable<string> UssFiles()
        {
            foreach (string path in Directory.GetFiles(UiDirectory, "*.uss"))
                yield return path;
        }

        [Test]
        public void EveryUssFileIsFound()
        {
            var files = new List<string>(UssFiles());
            Assert.That(files, Is.Not.Empty, "USS를 하나도 못 찾았다 — 경로 탐색이 깨졌다.");
        }

        [Test]
        public void FontSizes_AreMultiplesOfNine()
        {
            var violations = new List<string>();
            var rx = new Regex(@"font-size:\s*([0-9]*\.?[0-9]+)px", RegexOptions.Compiled);

            foreach (string path in UssFiles())
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (Match m in rx.Matches(lines[i]))
                    {
                        double value = double.Parse(
                            m.Groups[1].Value,
                            System.Globalization.CultureInfo.InvariantCulture);
                        if (Array.IndexOf(AllowedFontSizes, (int)value) >= 0 &&
                            Math.Abs(value - (int)value) < 0.0001)
                            continue;
                        violations.Add(
                            $"{Path.GetFileName(path)}:{i + 1}  font-size: {m.Groups[1].Value}px");
                    }
                }
            }

            Assert.That(violations, Is.Empty,
                "폰트 크기는 var(--pc-fs-*) 토큰(9/18/27/36/54)만 쓴다:\n  "
                + string.Join("\n  ", violations));
        }

        [Test]
        public void NoSyntheticBold()
        {
            var violations = new List<string>();
            foreach (string path in UssFiles())
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains("-unity-font-style") &&
                        (lines[i].Contains("bold") || lines[i].Contains("bold-and-italic")))
                        violations.Add($"{Path.GetFileName(path)}:{i + 1}  {lines[i].Trim()}");
                }
            }

            Assert.That(violations, Is.Empty,
                "Galmuri9에는 볼드 페이스가 없다. 강조는 색이나 크기로 한다:\n  "
                + string.Join("\n  ", violations));
        }

        [Test]
        public void NoFractionalLetterSpacing()
        {
            var violations = new List<string>();
            var rx = new Regex(@"letter-spacing:\s*(-?[0-9]*\.[0-9]+)px", RegexOptions.Compiled);

            foreach (string path in UssFiles())
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (Match m in rx.Matches(lines[i]))
                        violations.Add(
                            $"{Path.GetFileName(path)}:{i + 1}  letter-spacing: {m.Groups[1].Value}px");
                }
            }

            Assert.That(violations, Is.Empty,
                "소수점 자간은 도트 격자에 안 떨어진다. 정수만 쓴다:\n  "
                + string.Join("\n  ", violations));
        }
    }
}
