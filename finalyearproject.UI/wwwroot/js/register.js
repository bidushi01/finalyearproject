const validationGroupName = "RegisterForm";

$(function () {
    initRegisterForm();
});

function initRegisterForm() {

    // Validation Summary
    $("#ValidationSummary").dxValidationSummary({
        validationGroup: validationGroupName
    });

    // Username
    $("#txtUsername").dxTextBox({
        placeholder: "Enter username",
        onValueChanged: function (e) {
            $("#hiddenUsername").val(e.value);
        }
    }).dxValidator({
        validationGroup: validationGroupName,
        validationRules: [
            { type: "required", message: "Username is required" },
            {
                type: "stringLength",
                max: 50,
                message: "Username cannot exceed 50 characters"
            }
        ]
    });

    // Email
    $("#txtEmail").dxTextBox({
        placeholder: "Enter email",
        onValueChanged: function (e) {
            $("#hiddenEmail").val(e.value);
        }
    }).dxValidator({
        validationGroup: validationGroupName,
        validationRules: [
            { type: "required", message: "Email is required" },
            { type: "email", message: "Invalid email format" }
        ]
    });

    // Phone Number (10 digits)
    $("#txtPhone").dxTextBox({
        placeholder: "Enter 10-digit phone number",
        mask: "0000000000",
        onValueChanged: function (e) {
            $("#hiddenPhone").val(e.value);
        }
    }).dxValidator({
        validationGroup: validationGroupName,
        validationRules: [
            { type: "required", message: "Phone number is required" },
            {
                type: "pattern",
                pattern: /^\d{10}$/,
                message: "Phone must be exactly 10 digits"
            }
        ]
    });

    // Password
    $("#txtPassword").dxTextBox({
        mode: "password",
        placeholder: "Enter password",
        onValueChanged: function (e) {
            $("#hiddenPassword").val(e.value);
        }
    }).dxValidator({
        validationGroup: validationGroupName,
        validationRules: [
            { type: "required", message: "Password is required" },
            {
                type: "stringLength",
                min: 6,
                message: "Password must be at least 6 characters"
            }
        ]
    });

    // Confirm Password
    $("#txtConfirmPassword").dxTextBox({
        mode: "password",
        placeholder: "Confirm password",
        onValueChanged: function (e) {
            $("#hiddenConfirmPassword").val(e.value);
        }
    }).dxValidator({
        validationGroup: validationGroupName,
        validationRules: [
            { type: "required", message: "Please confirm your password" },
            {
                type: "compare",
                comparisonTarget: function () {
                    return $("#txtPassword").dxTextBox("instance").option("value");
                },
                message: "Passwords do not match"
            }
        ]
    });

    // Next Button (Step 1 → Step 2)
    $("#btnRegister").dxButton({
        text: "Continue to Email Verification →",
        type: "default",
        width: "100%",
        height: 50,
        stylingMode: "contained",
        onClick: function () {
            const result = DevExpress.validationEngine.validateGroup(validationGroupName);

            if (!result.isValid) {
                return;
            }

            // Submit form
            $("#registerForm").submit();
        }
    });
}