using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Attachment;
using ProjectManager.API.Validators.AttachmentValidators;

namespace ProjectManager.Tests.Validators
{
    public class PresignedUrlRequestDtoValidatorTests
    {
        private readonly PresignedUrlRequestDtoValidator _validator = new();

        private static PresignedUrlRequestDto Valid() => new()
        {
            FileName = "terv.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1024
        };

        //FileName

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void FileName_Empty_ShouldHaveError(string fileName)
        {
            var dto = Valid();
            dto.FileName = fileName;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.FileName);
        }

        [Fact]
        public void FileName_Exactly255Chars_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.FileName = new string('a', 255);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.FileName);
        }

        [Fact]
        public void FileName_Exactly256Chars_ShouldHaveError()
        {
            var dto = Valid();
            dto.FileName = new string('a', 256);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.FileName);
        }

        //ContentType

        [Fact]
        public void ContentType_Empty_ShouldHaveError()
        {
            var dto = Valid();
            dto.ContentType = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.ContentType);
        }

        [Fact]
        public void ContentType_Exactly120Chars_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.ContentType = new string('a', 120);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.ContentType);
        }

        [Fact]
        public void ContentType_Exactly121Chars_ShouldHaveError()
        {
            var dto = Valid();
            dto.ContentType = new string('a', 121);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.ContentType);
        }

        //A megengedett MIME típusokat nem itt szűrjük: az az AttachmentOptions dolga,
        //hogy a konfigurációból dőljön el, ne egy beégetett listából.
        [Theory]
        [InlineData("application/pdf")]
        [InlineData("image/png")]
        [InlineData("application/octet-stream")]
        public void ContentType_AnyMimeType_ShouldNotHaveError(string contentType)
        {
            var dto = Valid();
            dto.ContentType = contentType;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.ContentType);
        }

        //SizeBytes

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(long.MinValue)]
        public void SizeBytes_NotPositive_ShouldHaveError(long size)
        {
            var dto = Valid();
            dto.SizeBytes = size;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.SizeBytes);
        }

        [Fact]
        public void SizeBytes_One_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.SizeBytes = 1;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.SizeBytes);
        }

        //A felső határ a konfigurált maximum, amit a szolgáltatás érvényesít - a validátor nem
        [Fact]
        public void SizeBytes_VeryLarge_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.SizeBytes = long.MaxValue;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.SizeBytes);
        }

        [Fact]
        public void ValidDto_ShouldNotHaveAnyErrors()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
        }
    }
}
