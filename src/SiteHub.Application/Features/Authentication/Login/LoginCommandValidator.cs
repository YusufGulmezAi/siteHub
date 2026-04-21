using FluentValidation;

namespace SiteHub.Application.Features.Authentication.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Input)
            .NotEmpty().WithMessage("Kullanıcı bilgisi (TCKN, email, telefon veya VKN) zorunludur.")
            .MaximumLength(320).WithMessage("Kullanıcı bilgisi çok uzun.");

        RuleFor(c => c.Password)
            .NotEmpty().WithMessage("Parola zorunludur.")
            .MinimumLength(6).WithMessage("Parola en az 6 karakter olmalı.")
            .MaximumLength(200).WithMessage("Parola çok uzun.");

        RuleFor(c => c.ClientContext).NotNull();
        RuleFor(c => c.ClientContext.IpAddress)
            .NotEmpty().WithMessage("IP bilgisi eksik.");
    }
}
