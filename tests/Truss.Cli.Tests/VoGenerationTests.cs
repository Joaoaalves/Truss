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
        public void GenerateAggregate_WithRuleSegments_ResolvesRangesAndComparators()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run(
                "g", "agg", "Food", "-c", "Nutrition",
                "--vo", "Name:string:3..120", "--vo", "Calories:int:0..900", "--vo", "Fat:decimal:pos", "--project", root));

            var name = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Nutrition", "Food", "ValueObjects", "FoodName", "FoodName.cs");
            Assert.Contains("public const int MinLength = 3;", name);
            Assert.Contains("public const int MaxLength = 120;", name);
            Assert.Contains("CheckRule(new FoodNameMustNotBeTooShort(normalized));", name);

            var atMost = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Nutrition", "Food", "ValueObjects", "FoodCalories", "Rules", "FoodCaloriesMustBeAtMost.cs");
            Assert.Contains("value > 900", atMost);
            Assert.Contains("\"foodCalories.too-large\"", atMost);

            var positive = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Nutrition", "Food", "ValueObjects", "FoodFat", "Rules", "FoodFatMustBePositive.cs");
            Assert.Contains("value <= 0", positive);

            // The generated invalid samples respect the custom bounds.
            var caloriesTest = _workspace.ReadFile("Shop", "tests", "Shop.Domain.Tests", "Nutrition", "FoodCaloriesTests.cs");
            Assert.Contains("FoodCalories.Create(-1)", caloriesTest);

            var fatTest = _workspace.ReadFile("Shop", "tests", "Shop.Domain.Tests", "Nutrition", "FoodFatTests.cs");
            Assert.Contains("FoodFat.Create(0m)", fatTest);
        }

        [Fact]
        public void GenerateValueObject_Composite_BuildsTheMembersBesideIt()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("g", "agg", "Food", "-c", "Nutrition", "--project", root));
            Assert.Equal(0, _workspace.Run(
                "g", "vo", "MacroNutrients", "-c", "Nutrition", "-a", "Food",
                "--vo", "Carbohydrates:decimal", "--vo", "Protein:decimal", "--project", root));

            var composite = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Nutrition", "Food", "ValueObjects", "MacroNutrients", "MacroNutrients.cs");
            Assert.Contains("namespace Shop.Domain.Nutrition.Food.ValueObjects.MacroNutrients", composite);
            Assert.Contains("public static MacroNutrients Create(Carbohydrates carbohydrates, Protein protein)", composite);
            Assert.Contains("public static MacroNutrients Create(decimal carbohydrates, decimal protein)", composite);
            Assert.Contains("// Rules that read several members belong here.", composite);
            Assert.DoesNotContain("Calories()", composite);

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Nutrition", "Food", "ValueObjects", "Carbohydrates", "Carbohydrates.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Nutrition", "Food", "ValueObjects", "Protein", "Rules", "ProteinMustNotBeNegative.cs"));

            var test = _workspace.ReadFile("Shop", "tests", "Shop.Domain.Tests", "Nutrition", "MacroNutrientsTests.cs");
            Assert.Contains("MacroNutrients.Create(10m, 10m)", test);
        }

        [Fact]
        public void GenerateValueObject_BoundToAnEntity_LandsInItsFolder()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("g", "ent", "Warehouse", "-c", "Logistics", "--project", root));
            Assert.Equal(0, _workspace.Run(
                "g", "vo", "Capacity", "-c", "Logistics", "-a", "Warehouse",
                "-f", "Value:int:pos", "--project", root));

            var vo = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Logistics", "Warehouse", "ValueObjects", "Capacity", "Capacity.cs");
            Assert.Contains("namespace Shop.Domain.Logistics.Warehouse.ValueObjects.Capacity", vo);
            Assert.Contains("CheckRule(new CapacityMustBePositive(value));", vo);
        }

        [Fact]
        public void GenerateEntity_ReferencingAnExistingValueObject_UsesIt()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("g", "vo", "Money", "-c", "Shared", "-f", "Amount:decimal", "--project", root));
            Assert.Equal(0, _workspace.Run("g", "agg", "Order", "-c", "Sales", "--project", root));
            Assert.Equal(0, _workspace.Run("g", "ent", "OrderItem", "-c", "Sales", "-a", "Order", "--vo", "Price:Money", "--project", root));

            var entity = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Sales", "Order", "OrderItem.cs");
            Assert.Contains("using Shop.Domain.Shared.ValueObjects.Money;", entity);
            Assert.Contains("public Money Price { get; private set; }", entity);
            Assert.Contains("public OrderItem(OrderItemId id, Money price) : base(id)", entity);
        }

        [Fact]
        public void GenerateValueObject_BoundToAMissingAggregate_Fails()
        {
            var root = ScaffoldShop();

            Assert.Equal(1, _workspace.Run("g", "vo", "MacroNutrients", "-c", "Nutrition", "-a", "Food", "--vo", "Protein:decimal", "--project", root));
        }

        [Fact]
        public void GenerateAggregate_ReferencingAnExistingValueObject_UsesItWithoutRegenerating()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("g", "vo", "Money", "-c", "Shared", "-f", "Amount:decimal", "--project", root));
            Assert.Equal(0, _workspace.Run("g", "agg", "Product", "-c", "Sales", "--vo", "Name:string", "--vo", "Price:Money", "--project", root));

            var aggregate = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Sales", "Product", "Product.cs");
            Assert.Contains("using Shop.Domain.Shared.ValueObjects.Money;", aggregate);
            Assert.Contains("public Money Price { get; private set; }", aggregate);
            Assert.Contains("public static Product Create(ProductName name, Money price)", aggregate);

            Assert.False(Directory.Exists(Path.Combine(root, "src", "Shop.Domain", "Sales", "Product", "ValueObjects", "Money")));

            // A referenced value object cannot be flattened into a crud slice yet.
            Assert.Equal(1, _workspace.Run("g", "agg", "Order", "-c", "Sales", "--crud", "--vo", "Total:Money", "--project", root));
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
