// 🔧 GESTIÓN DE SESIONES EN EL CLIENTE
$(document).ready(function () {
    // Interceptar todas las peticiones AJAX
    $(document).ajaxComplete(function (event, xhr, settings) {
        try {
            var response = JSON.parse(xhr.responseText);

            if (response && response.sessionExpired === true) {
                alert('Su sesión ha expirado. La página se recargará automáticamente.');
                window.location.href = '/Usuarios/Login';
                return;
            }
        } catch (e) {
            // No es JSON, continuar normal
        }

        // Verificar códigos de estado de autenticación
        if (xhr.status === 401 || xhr.status === 403) {
            alert('Su sesión ha expirado. Por favor, inicie sesión nuevamente.');
            window.location.href = '/Usuarios/Login';
        }
    });

    // 🕒 RENOVACIÓN AUTOMÁTICA DE SESIÓN (cada 25 minutos)
    setInterval(function () {
        $.ajax({
            url: '/Usuarios/RenewSession',
            type: 'POST',
            cache: false,
            success: function (response) {
                if (!response.success) {
                    window.location.href = '/Usuarios/Login';
                }
            },
            error: function () {
                // Si falla la renovación, redirigir al login
                window.location.href = '/Usuarios/Login';
            }
        });
    }, 25 * 60 * 1000); // 25 minutos
});