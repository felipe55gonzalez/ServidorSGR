# ServidorSGR

ServidorSGR es un servidor de comunicación en tiempo real construido con ASP.NET Core SignalR. Facilita el intercambio de datos entre clientes conectados, permitiéndoles registrar alias únicos y enviarse lotes de datos entre ellos. Este servidor está diseñado para funcionar en conjunto con el proyecto [ClienteSGR](https://github.com/felipe55gonzalez/ClienteSGR).

## Características

* **Comunicación Bidireccional en Tiempo Real:** Utiliza SignalR para la transferencia instantánea de datos entre el servidor y los clientes.
* **Registro de Alias de Cliente:** Los clientes pueden registrar un alias único en el servidor, facilitando la identificación y el envío de mensajes a destinatarios específicos.
* **Despacho de Datos Dirigido:** Permite a los clientes enviar objetos `DataBatchContainer` a otros clientes especificando el alias del destinatario.
* **Serialización Eficiente de Datos:** Emplea MessagePack para serializar y deserializar mensajes, ofreciendo un mejor rendimiento y tamaños de mensaje más pequeños en comparación con JSON.
* **Gestión de Conexiones:** Maneja las conexiones y desconexiones de los clientes de forma elegante, incluyendo el mapeo de IDs de conexión a alias.
* **Base Escalable:** Construido sobre .NET 8 para el desarrollo de aplicaciones del lado del servidor robustas y modernas.

## Tecnologías Utilizadas

* **.NET 8**
* **ASP.NET Core SignalR**
* **MessagePack** (para el protocolo SignalR y los modelos de datos)
* **Swashbuckle.AspNetCore** (para la documentación de API, aunque no se usa explícitamente para los hubs de SignalR en este contexto, es una dependencia listada)

## Estructura del Proyecto

* `ServidorSGR/`
    * `Hubs/`
        * `DataHub.cs`: El hub principal de SignalR responsable de manejar las conexiones de clientes, desconexiones, registro de alias (`RegisterAlias`) y transmisión de datos (`SendData`).
    * `Models/`
        * `DataBatch.cs`: Define la estructura para paquetes de datos individuales, incluyendo metadatos como `Type` (tipo de mensaje), `TransferId`, `Sequence`, `IsFirst` (indicador de primer paquete), `IsLast` (indicador de último paquete), `Filename`, `OriginalSize`, y la carga útil de `Data`. También incluye campos para información de paquetes IP (`SourceIp`, `DestinationIp`, `ProtocolType`).
        * `DataBatchContainer.cs`: Un contenedor para una lista de objetos `DataBatch`, utilizado para enviar múltiples lotes a la vez.
        * `RegisterAliasRequest.cs`: Modelo para la solicitud de registro de alias, que contiene el `Alias` deseado.
        * `SendDataRequest.cs`: Modelo para la solicitud de envío de datos, que contiene el `RecipientAlias` y el `BatchContainer`.
    * `Services/`
        * `ConnectionMapping.cs`: Un servicio que implementa `IConnectionMapping` para gestionar la asociación entre los alias de los clientes y sus IDs de conexión de SignalR. Proporciona operaciones seguras para hilos para agregar, eliminar y recuperar estos mapeos.
    * `Program.cs`: El punto de entrada de la aplicación. Configura los servicios, incluyendo SignalR (con el protocolo MessagePack y el tamaño máximo de mensaje), el singleton `ConnectionMapping`, y mapea el `DataHub`.
    * `appsettings.json` & `appsettings.Development.json`: Archivos de configuración para la aplicación.
    * `Properties/launchSettings.json`: Define perfiles para lanzar la aplicación, incluyendo URLs y variables de entorno.

## Configuración y Ejecución del Servidor

### Prerrequisitos

* SDK de .NET 8 o posterior.

### Pasos

1.  **Clonar el repositorio:**
    ```bash
    git clone https://github.com/felipe55gonzalez/ServidorSGR
    cd ServidorSGR
    ```
2.  **Restaurar dependencias:**
    ```bash
    dotnet restore
    ```
3.  **Ejecutar el servidor:**
    ```bash
    dotnet run
    ```
    Por defecto (según `Properties/launchSettings.json`), el servidor podría ser accesible en:
    * `http://localhost:5137`
    * `https://localhost:7280`

## Endpoints

* **Hub de SignalR:** `/datahub`
    * Este es el endpoint principal al que los clientes se conectarán para la comunicación en tiempo real.
* **Raíz/Verificación de Estado:** `/`
    * Acceder a la URL raíz (ej., `http://localhost:5137/`) devolverá el texto "ServidorSGR is running." indicando que el servidor está operativo.

## Cómo Funciona

El servidor `ServidorSGR` opera como un intermediario (broker) de mensajes para los clientes conectados, permitiendo la comunicación en tiempo real basada en alias.

1.  **Conexión Inicial y Ciclo de Vida del Hub:**
    * Los clientes establecen una conexión persistente (generalmente WebSocket) con el servidor en el endpoint `/datahub`.
    * Cuando un cliente se conecta, se invoca el método `OnConnectedAsync` en `DataHub`. En la implementación actual, este método llama a la lógica base de SignalR, pero podría extenderse para lógicas de bienvenida o inicialización.

2.  **Registro de Alias (Método `RegisterAlias`):**
    * Un cliente que desea ser identificable por otros envía una solicitud al método `RegisterAlias` del hub. Esta solicitud contiene un objeto `RegisterAliasRequest` con el `Alias` deseado.
    * El `DataHub` procesa la solicitud:
        * **Validación:** Primero, verifica que el alias proporcionado no sea nulo o esté vacío. Si lo es, envía `AliasRegistrationFailed` al cliente solicitante.
        * **Verificación de Duplicados y Posesión:** Consulta el servicio `ConnectionMapping` para determinar si el alias ya está en uso.
            * Si el alias ya está registrado por *otro* ID de conexión, se envía `AliasRegistrationFailed` al cliente, indicando que el alias está ocupado.
            * Si el alias ya está registrado por el *mismo* ID de conexión (es decir, el cliente intenta registrar un alias que ya posee), la operación se considera exitosa (idempotencia).
        * **Mapeo:** Si el alias es válido y no está en uso por otro cliente, el servicio `ConnectionMapping` almacena la asociación entre el `alias` y el `Context.ConnectionId` del cliente. Este servicio utiliza internamente diccionarios concurrentes (`ConcurrentDictionary`) para garantizar la seguridad en entornos multi-hilo, manteniendo mapeos en ambas direcciones (`alias -> connectionId` y `connectionId -> alias`).
    * **Respuesta al Cliente:** El servidor notifica al cliente el resultado de la operación invocando `Clients.Caller.SendAsync("AliasRegistered", alias)` en caso de éxito, o `Clients.Caller.SendAsync("AliasRegistrationFailed", "Mensaje de error específico")` en caso de fallo.

3.  **Transmisión de Datos (Método `SendData`):**
    * Un cliente (remitente) que desea enviar datos a otro cliente (destinatario) invoca el método `SendData` en el hub. La solicitud (`SendDataRequest`) incluye el `RecipientAlias` (alias del destinatario) y un `BatchContainer` que contiene los datos a enviar.
    * El `DataHub` maneja la solicitud de envío:
        * **Validación:** Comprueba que `BatchContainer` no sea nulo, que `RecipientAlias` no esté vacío y que la lista `Batches` dentro del contenedor no esté vacía. Si falla la validación, se notifica al remitente con `SendMessageFailed`.
        * **Resolución del Destinatario:** Utiliza `ConnectionMapping` para obtener el `connectionId` asociado al `RecipientAlias`.
        * **Envío al Destinatario:**
            * Si no se encuentra el `connectionId` (el alias no existe o el cliente destinatario está desconectado), se envía `SendMessageFailed` al remitente.
            * Si se encuentra, el servidor reenvía el `BatchContainer` directamente al cliente destinatario específico utilizando su `connectionId`: `Clients.Client(recipientConnectionId).SendAsync("ReceiveDataBatch", request.BatchContainer)`. Es crucial que el cliente destinatario esté escuchando un método llamado `ReceiveDataBatch` para procesar los datos entrantes.
            * El objeto `BatchContainer` puede contener una lista de `DataBatch`, cada uno con metadatos como tipo, ID de transferencia, secuencia, indicadores de inicio/fin, nombre de archivo, tamaño original y los datos binarios (`byte[]`), además de información de paquetes IP. Esto sugiere que el servidor actúa como un relé para estructuras de datos potencialmente complejas o fragmentadas, útil para transferencias de archivos o encapsulación de paquetes de red.
        * **Manejo de Errores:** Si ocurre una excepción durante el intento de envío al destinatario (ej., el cliente se desconecta abruptamente), se captura y se notifica al remitente con `SendMessageFailed`.

4.  **Serialización de Mensajes con MessagePack:**
    * La configuración en `Program.cs` (`.AddMessagePackProtocol()`) establece MessagePack como el protocolo de serialización para SignalR.
    * Los modelos de datos clave como `DataBatch`, `DataBatchContainer`, `RegisterAliasRequest`, y `SendDataRequest` están decorados con atributos `[MessagePackObject]` y `[Key(index)]`. Esto permite a MessagePack serializarlos y deserializarlos de manera muy eficiente, resultando en cargas útiles más pequeñas y menor latencia en comparación con formatos como JSON.

5.  **Gestión de Desconexiones (Método `OnDisconnectedAsync`):**
    * Cuando la conexión de un cliente con el servidor se interrumpe (ya sea de forma ordenada o inesperada), SignalR invoca automáticamente el método `OnDisconnectedAsync` en `DataHub`.
    * Dentro de este método, el servidor utiliza `ConnectionMapping` para obtener el alias asociado al `Context.ConnectionId` del cliente que se ha desconectado.
    * Si se encuentra un alias, se llama a `_connectionMapping.RemoveByConnectionId(Context.ConnectionId)` para eliminar las entradas de mapeo del cliente de ambos diccionarios internos en `ConnectionMapping`. Esto libera el alias para que pueda ser utilizado por otro cliente y mantiene limpio el estado del servidor.

6.  **Configuración del Servidor (`Program.cs`):**
    * **Inyección de Dependencias:** El servicio `ConnectionMapping` se registra como un singleton (`builder.Services.AddSingleton<IConnectionMapping, ConnectionMapping>()`), asegurando que una única instancia gestione todos los mapeos de conexión para la aplicación.
    * **Tamaño Máximo de Mensaje:** Se configura un tamaño máximo de recepción de mensaje de 10MB (`options.MaximumReceiveMessageSize = 10 * 1024 * 1024;`). Esto es importante para permitir la transferencia de los objetos `DataBatchContainer` que podrían contener cantidades significativas de datos binarios.

7.  **Rol del Servicio `ConnectionMapping`:**
    * Este servicio es fundamental para el funcionamiento del sistema de alias. Actúa como un registro centralizado y seguro para hilos (gracias al uso de `ConcurrentDictionary`) de los alias activos y sus correspondientes IDs de conexión de SignalR.
    * Mantiene dos diccionarios para búsquedas eficientes: uno para buscar `connectionId` por `alias` y otro para buscar `alias` por `connectionId`. Esto es crucial para el enrutamiento de mensajes y la limpieza durante las desconexiones.

## Cliente

Este servidor está destinado a ser utilizado con el proyecto `ClienteSGR`, que se puede encontrar aquí:
[felipe55gonzalez/ClienteSGR](https://github.com/felipe55gonzalez/ClienteSGR)

## Contribuciones

¡Las contribuciones son bienvenidas! Si deseas mejorar este proyecto, considera lo siguiente:

* **Reportar Bugs:** Si encuentras un error, por favor, abre un "issue" detallando el problema, los pasos para reproducirlo y cualquier información relevante de tu entorno.
* **Sugerir Mejoras o Nuevas Características:** Si tienes ideas para nuevas funcionalidades o mejoras a las existentes, puedes abrir un "issue" para discutirlo.
* **Mejorar la Documentación:** Si ves áreas donde la documentación puede ser más clara o más completa, tus aportes son muy valiosos.
* **Enviar Pull Requests:** Para cambios en el código o la documentación:
    1.  Realiza un "fork" del repositorio.
    2.  Crea una nueva rama para tus cambios (ej., `git checkout -b feature/nueva-caracteristica` o `bugfix/descripcion-del-bug`).
    3.  Realiza tus modificaciones y haz "commit" de tus cambios.
    4.  Envía tus cambios a tu "fork" (`git push origin tu-rama`).
    5.  Abre un "Pull Request" desde tu rama hacia la rama principal del repositorio original.

Se agradece cualquier tipo de contribución que ayude a mejorar el proyecto.

## Licencia

Este proyecto está licenciado bajo la Licencia MIT. Consulta el archivo `LICENSE` para más detalles, o revisa la licencia a continuación:
