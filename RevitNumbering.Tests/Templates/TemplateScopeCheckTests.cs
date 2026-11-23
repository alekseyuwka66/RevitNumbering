using RevitNumbering.Core.Model;
using RevitNumbering.Core.Templates;
using static RevitNumbering.Tests.Templates.TemplateTestData;

namespace RevitNumbering.Tests.Templates
{
    /// <summary>
    /// Scope-dependent checks.
    /// </summary>
    public class TemplateScopeCheckTests
    {
        private const string NoLevel = "Без уровня";

        [Fact]
        public void ManyLevelsWithoutLevelPlaceholder()
        {
            var issue = TemplateScopeCheck.CheckLevelCount(Template("Д-{N:000}"), new[] { "L2", "L1", "L2" },
                TwoLevels());

            AssertIssue(issue, TemplateIssueCode.MissingLevelForManyLevels, "Этаж 1", "Этаж 2");
            Assert.False(issue.IsWarning);
            AssertIssueText("В охвате несколько уровней — добавьте {Уровень}", issue);
        }

        [Fact]
        public void NoLevelIsSeparateLevel()
        {
            var template = Template("Д-{N:000}");
            var levels = TwoLevels();

            AssertIssue(TemplateScopeCheck.CheckLevelCount(template, new[] { "L1", null }, levels),
                TemplateIssueCode.MissingLevelForManyLevels, "Этаж 1", NoLevel);
            AssertIssue(TemplateScopeCheck.CheckLevelCount(template, new[] { "L1", "X9" }, levels),
                TemplateIssueCode.MissingLevelForManyLevels, "Этаж 1", NoLevel);
            Assert.Null(TemplateScopeCheck.CheckLevelCount(template, new string[] { null, null }, levels));
            Assert.Null(TemplateScopeCheck.CheckLevelCount(template, new[] { "L1", "L1" }, levels));
            Assert.Null(TemplateScopeCheck.CheckLevelCount(template, new string[0], levels));
        }

        [Fact]
        public void WithLevelPlaceholder_AnyNumberOfLevels()
        {
            var issue = TemplateScopeCheck.CheckLevelCount(Template("Д-{Уровень}-{N:000}"),
                new[] { "L1", "L2", null }, TwoLevels());

            Assert.Null(issue);
        }

        [Fact]
        public void WidthTooSmall()
        {
            var template = Template("Д-{Уровень}-{N:00}");

            var issue = TemplateScopeCheck.CheckWidth(template, 150);

            AssertIssue(issue, TemplateIssueCode.WidthTooSmall, "2", "150");
            Assert.True(issue.IsWarning);
            AssertIssueText("Разрядности 2 не хватает: на уровне 150 номеров", issue);
            Assert.Null(TemplateScopeCheck.CheckWidth(template, 99));
            AssertIssue(TemplateScopeCheck.CheckWidth(template, 100), TemplateIssueCode.WidthTooSmall, "2", "100");
        }

        [Fact]
        public void WarningOnlyForWidthOfTwoOrMore()
        {
            Assert.Null(TemplateScopeCheck.CheckWidth(Template("{N}"), 1000));
            Assert.Null(TemplateScopeCheck.CheckWidth(Template("{N:0}"), 9));
            Assert.Null(TemplateScopeCheck.CheckWidth(Template("{N:0}"), 10));
            Assert.Null(TemplateScopeCheck.CheckWidth(Template("{N:000}"), 0));
            AssertIssue(TemplateScopeCheck.CheckWidth(Template("{N:000}"), 1000),
                TemplateIssueCode.WidthTooSmall, "3", "1000");
        }

        [Fact]
        public void FtExample_LevelPartMissing()
        {
            var levels = new[] { Level("01 этаж +0.000", 0), Level("Кровля", 9000) };

            var issue = TemplateScopeCheck.CheckLevelCodes(Template("Д-{Уровень}-{N:000}"),
                NameRule(LevelCodePart.WordN, 2), levels);

            AssertIssue(issue, TemplateIssueCode.LevelCodePartMissing, "Кровля");
            Assert.False(issue.IsWarning);
            AssertIssueText("нет части: Кровля", issue);
        }

        [Fact]
        public void LevelList_OrderAndRepeats()
        {
            var levels = new[]
            {
                Level("Кровля", 9000, "B"),
                Level("Подвал", -3000, "A"),
                Level("Тех этаж", 6000, "C"),
                Level("Кровля", 9000, "B")
            };

            var issue = TemplateScopeCheck.CheckLevelCodes(Template("Д-{Уровень}-{N:000}"),
                NameRule(LevelCodePart.WordN, 2), levels);

            AssertIssue(issue, TemplateIssueCode.LevelCodePartMissing, "Подвал", "Кровля");
            AssertIssueText("нет части: Подвал, Кровля", issue);
        }

        [Fact]
        public void TemplateWithoutLevel_NoLevelCodeCheck()
        {
            var issue = TemplateScopeCheck.CheckLevelCodes(Template("Д-{N:000}"),
                NameRule(LevelCodePart.WordN, 2), new[] { Level("Кровля") });

            Assert.Null(issue);
        }

        [Fact]
        public void EveryIssueHasItsOwnText()
        {
            var issues = new List<TemplateIssue>
            {
                TemplateParser.Parse("Д-{Уровень-{N}").Issues.Single(),
                TemplateParser.Parse("Д-{Уровнь}-{N}").Issues.Single(),
                TemplateParser.Parse("Д-").Issues.Single(),
                TemplateParser.Parse("{N:00a}").Issues.Single(),
                TemplateScopeCheck.CheckLevelCount(Template("Д-{N:000}"), new[] { "L2", "L1", "L2" }, TwoLevels()),
                TemplateScopeCheck.CheckLevelCodes(Template("Д-{Уровень}-{N:000}"),
                    NameRule(LevelCodePart.WordN, 2), new LevelData[] { Level("Кровля"), Level("Подвал") }),
                TemplateScopeCheck.CheckWidth(Template("Д-{Уровень}-{N:00}"), 150)
            };
            var expectedTexts = new[]
            {
                "Незакрытая скобка",
                "Неизвестная подстановка: {Уровнь}",
                "Нет порядкового номера {N}",
                "Неверная разрядность: {N:00a}",
                "В охвате несколько уровней — добавьте {Уровень}",
                "нет части: Кровля, Подвал",
                "Разрядности 2 не хватает: на уровне 150 номеров"
            };

            Assert.Equal(7, issues.Select(issue => issue.Code).Distinct().Count());
            Assert.Equal(new[] { "Кровля", "Подвал" }, issues[5].Arguments);
            Assert.Equal(7, expectedTexts.Distinct(StringComparer.Ordinal).Count());
            for (int i = 0; i < issues.Count; i++)
            {
                AssertIssueText(expectedTexts[i], issues[i]);
            }
        }
    }
}
