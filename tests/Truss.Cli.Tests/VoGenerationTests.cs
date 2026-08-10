using Xunit;

namespace Truss.Cli.Tests
{
    public class VoGenerationTests : IDisposable
    {
        private readonly CliTestWorkspace _workspace = new();

        private string ScaffoldShop()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));
            return _workspace.Root("Shop");
        }

        [Fact]
        public void GenerateAggregate_WithVos_WrapsEveryPrimitiveInAValueObject()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run(
                "g", "agg", "Food", "-c", "Nutrition",
                "--vo", "Name:string", "--vo", "Calories:int", "--project", root));

            var aggregate = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Nutrition", "Food", "Food.cs");
            Assert.Contains("public FoodName Name { get; private set; }", aggregate);
            Assert.Contains("public FoodCalories Calories { get; private set; }", aggregate);
            Assert.Contains("public static Food Create(FoodName name, FoodCalories calories)", aggregate);
            Assert.DoesNotContain("string Name", aggregate);

            var vo = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Nutrition", "Food", "ValueObjects", "FoodName", "FoodName.cs");
            Assert.Contains("namespace Shop.Domain.Nutrition.Food.ValueObjects.FoodName", vo);
            Assert.Contains("public sealed class FoodName : ValueObject", vo);
            Assert.Contains("CheckRule(new FoodNameMustNotBeEmpty(normalized));", vo);
            Assert.Contains("value?.Trim() ?? string.Empty", vo);

            var rule = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Nutrition", "Food", "ValueObjects", "FoodCalories", "Rules", "FoodCaloriesMustNotBeNegative.cs");
            Assert.Contains("value < 0", rule);
            Assert.Contains("\"foodCalories.negative\"", rule);

            Assert.True(_workspace.FileExists("Shop", "tests", "Shop.Domain.Tests", "Nutrition", "FoodNameTests.cs"));
            Assert.True(_workspace.FileExists("Shop", "tests", "Shop.Domain.Tests", "Nutrition", "FoodCaloriesTests.cs"));

            var aggregateTest = _workspace.ReadFile("Shop", "tests", "Shop.Domain.Tests", "Nutrition", "FoodTests.cs");
            Assert.Contains("Food.Create(FoodName.Create(\"Beam\"), FoodCalories.Create(10))", aggregateTest);
        }

        [Fact]
        public void GenerateAggregate_WithVosAndCrud_SpeaksValueObjectsBehindAPrimitiveBoundary()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run(
                "g", "agg", "Food", "-c", "Nutrition", "--crud",
                "--vo", "Name:string,Calories:int", "--project", root));

            var command = _workspace.ReadFile("Shop", "src", "Shop.Application", "Nutrition", "Food", "CreateFood", "CreateFood.cs");
            Assert.Contains("CreateFood(string Name, int Calories)", command);

            var handler = _workspace.ReadFile("Shop", "src", "Shop.Application", "Nutrition", "Food", "CreateFood", "CreateFoodHandler.cs");
            Assert.Contains("FoodName.Create(command.Name)", handler);
            Assert.Contains("FoodCalories.Create(command.Calories)", handler);

            var update = _workspace.ReadFile("Shop", "src", "Shop.Application", "Nutrition", "Food", "UpdateFood", "UpdateFoodHandler.cs");
            Assert.Contains("food.Rename(FoodName.Create(command.Name));", update);
            Assert.Contains("food.ChangeCalories(FoodCalories.Create(command.Calories));", update);

            var validator = _workspace.ReadFile("Shop", "src", "Shop.Application", "Nutrition", "Food", "CreateFood", "CreateFoodValidator.cs");
            Assert.Contains("MaximumLength(FoodName.MaxLength)", validator);

            var configuration = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "Nutrition", "FoodConfiguration.cs");
            Assert.Contains(".HasConversion(name => name.Value, value => FoodName.Create(value))", configuration);
            Assert.Contains(".HasMaxLength(FoodName.MaxLength)", configuration);
            Assert.Contains(".HasConversion(calories => calories.Value, value => FoodCalories.Create(value));", configuration);

            var repository = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "Nutrition", "EfFoodRepository.cs");
            Assert.Contains("new FoodDto(food.Id.Value, food.Name.Value, food.Calories.Value)", repository);

            var integration = _workspace.ReadFile("Shop", "tests", "Shop.IntegrationTests", "Nutrition", "FoodCrudTests.cs");
            Assert.Contains("new CreateFood(\"Beam\", 10)", integration);
            Assert.Contains("new UpdateFood(id, \"Joist\", 20)", integration);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("app.MapCommand<CreateFood, Guid>(\"/foods\"", program);
        }

        [Fact]
        public void GenerateEntity_WithVos_PlacesThemInTheOwnersFolder()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("g", "agg", "Food", "-c", "Nutrition", "--project", root));
            Assert.Equal(0, _workspace.Run("g", "ent", "Portion", "-c", "Nutrition", "-a", "Food", "--vo", "Grams:decimal", "--project", root));

            var entity = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Nutrition", "Food", "Portion.cs");
            Assert.Contains("public PortionGrams Grams { get; private set; }", entity);
            Assert.Contains("public Portion(PortionId id, PortionGrams grams) : base(id)", entity);

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Nutrition", "Food", "ValueObjects", "PortionGrams", "PortionGrams.cs"));
        }

        [Fact]
        public void GenerateValueObject_Standalone_SupportsSeveralFields()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("g", "vo", "Money", "-c", "Shared", "-f", "Amount:decimal", "-f", "Currency:string", "--project", root));

            var vo = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Shared", "ValueObjects", "Money", "Money.cs");
            Assert.Contains("namespace Shop.Domain.Shared.ValueObjects.Money", vo);
            Assert.Contains("public static Money Create(decimal amount, string currency)", vo);
            Assert.Contains("public const int CurrencyMaxLength = 200;", vo);
            Assert.Contains("yield return Amount;", vo);

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Shared", "ValueObjects", "Money", "Rules", "MoneyCurrencyMustNotBeEmpty.cs"));

            var test = _workspace.ReadFile("Shop", "tests", "Shop.Domain.Tests", "Shared", "MoneyTests.cs");
            Assert.Contains("Create_WithAnInvalidAmount_BreaksTheRule", test);
            Assert.Contains("Create_WithAnInvalidCurrency_BreaksTheRule", test);
        }

        [Fact]
        public void GenerateAggregate_WithUintVo_WrapsAnIntInstead()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("g", "agg", "Food", "-c", "Nutrition", "--vo", "Calories:uint", "--project", root));

            var vo = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Nutrition", "Food", "ValueObjects", "FoodCalories", "FoodCalories.cs");
            Assert.Contains("public static FoodCalories Create(int value)", vo);
            Assert.DoesNotContain("uint", vo);
        }

        [Fact]
        public void GenerateAggregate_WithAnUnknownVoType_FailsWithGuidance()
        {
            var root = ScaffoldShop();

            Assert.Equal(1, _workspace.Run("g", "agg", "Food", "-c", "Nutrition", "--vo", "Name:varchar", "--project", root));

            Assert.False(_workspace.FileExists("Shop", "src", "Shop.Domain", "Nutrition", "Food", "Food.cs"));
        }

        public void Dispose() => _workspace.Dispose();
    }
}
