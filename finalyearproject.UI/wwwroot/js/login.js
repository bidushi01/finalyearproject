const loginValidationGroup = "LoginForm";

$(function () {
    initLoginForm();
});

function initLoginForm() {

    $("#LoginValidationSummary").dxValidationSummary({
        validationGroup: loginValidationGroup
    });

    $("#txtEmailOrUsername").dxTextBox({
        placeholder: "Enter email or username",
        onValueChanged: function (e) {
            $("#hiddenEmailOrUsername").val(e.value);
        }
    }).dxValidator({
        validationGroup: loginValidationGroup,
        validationRules: [
            { type: "required", message: "Email or Username is required" }
        ]
    });

    $("#txtLoginPassword").dxTextBox({
        mode: "password",
        placeholder: "Enter password",
        onValueChanged: function (e) {
            $("#hiddenLoginPassword").val(e.value);
        }
    }).dxValidator({
        validationGroup: loginValidationGroup,
        validationRules: [
            { type: "required", message: "Password is required" }
        ]
    });

    $("#btnLogin").dxButton({
        text: "Login",
        type: "default",
        width: "100%",
        height: 50,
        stylingMode: "contained",
        onClick: function () {
            const result = DevExpress.validationEngine.validateGroup(loginValidationGroup);

            if (!result.isValid) {
                return;
            }

            $("#loginForm").submit();
        }
    });
}