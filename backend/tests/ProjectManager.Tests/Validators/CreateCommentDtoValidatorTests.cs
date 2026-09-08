using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Comments;
using ProjectManager.API.Validators.CommentValidators;

namespace ProjectManager.Tests.Validators
{
    public class CreateCommentDtoValidatorTests
    {
        private readonly CreateCommentDtoValidator _validator = new();

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\n\t")]
        public void Body_Empty_ShouldHaveError(string body)
        {
            //A NotEmpty a csupa whitespace tartalmat is elutasítja
            _validator.TestValidate(new CreateCommentDto { Body = body })
                .ShouldHaveValidationErrorFor(x => x.Body);
        }

        [Fact]
        public void Body_Exactly2000Chars_ShouldNotHaveError()
        {
            _validator.TestValidate(new CreateCommentDto { Body = new string('a', 2000) })
                .ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Body_Exactly2001Chars_ShouldHaveError()
        {
            _validator.TestValidate(new CreateCommentDto { Body = new string('a', 2001) })
                .ShouldHaveValidationErrorFor(x => x.Body);
        }

        //A komment markupot tartalmazhat: a megjelenítéskor a Svelte escape-eli.
        //Itt szándékosan nincs karakter-tiltás, különben a kódrészletek beküldése lehetetlen lenne.
        [Theory]
        [InlineData("Ez egy komment.")]
        [InlineData("if (a < b && c > d) { }")]
        [InlineData("Lásd: <https://example.com>")]
        public void Body_Valid_ShouldNotHaveError(string body)
        {
            _validator.TestValidate(new CreateCommentDto { Body = body })
                .ShouldNotHaveAnyValidationErrors();
        }
    }
}
