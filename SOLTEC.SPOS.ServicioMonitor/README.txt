/*
	Application: Soltec Servicio de transmision.
*/


Date:			20240821
Author:			Juan Carlos Velazquez
Version:		v1.1.3
Description:	Se realiza cambio de logica para el registro de sucursal en la base de datos, se modifica logica para la revision del status del servicio. 
				Se actualizan scripts tipo bat para la manipulacion del servicion ya instalado en la maquina de la sucursal.
				Se crea script .bat para el proceso de descompresion de archivos y actualizacion de ensamblados; crea o iniciar servicion windows.
Release:		True

Date:			20240822
Author:			Juan Carlos Velazquez
Version:		v1.1.4
Description:	Se modifica obtencion de instancias para controlar opciones de conecciones a base de datos y poder obtener la sucursal.
Release:		False

Date:			20240824
Author:			Juan Carlos Velazquez
Version:		v1.1.5
Description:	Se crea script bar para la creacion de una tarea y reiniciar diariamente el servicio a las 10:10 am, se crea script bat para la eliminacion de la tarea creada.
Release:		False

Date:			20240825
Author:			Juan Carlos Velazquez
Version:		v1.1.6
Description:	Se actualiza script InstaladorServicioTransmision para incluir la creacion de una tarea.
Release:		True

Date:			20240826
Author:			Juan Carlos Velazquez
Version:		v1.1.7
Description:	Se modifica proyecto de DAL para la extender el tiempo de conexion de bases de datos.
Release:		false

Date:			20240826
Author:			Juan Carlos Velazquez
Version:		v1.1.8
Description:	Se implementa logica para el almacenamiento de la version que se tiene implementada en cada una de las versiones. Se modifica hora de la creacion de la tarea a la 10:00
Release:		True

Date:			20240829
Author:			Juan Carlos Velazquez
Version:		v1.1.9
Description:	Se modifica archivo json para remover las cadenas de conexion.


Date:			20240904
Author:			Juan Carlos Velazquez
Version:		v1.2.0
Description:	Se modifica toda logica para el registro de sucursales. Creando una Web API para la obtencion de configuracion y procesos. 
				Se crea proyecto web API para el uso del servicio y consultar procesos.
Release			True

Date:			20240909
Author:			Juan Carlos Velazquez
Version:		v1.2.1
Description:	Se modifican archivos bat para la validacion de sistema operativo y si es diferente de windows 10 u 11, descomprimira arhivos zip con win rar esto esperando que existe win rar instalado en las 
				maquinas que tengan un windows 7
Release			false

Date:			20240910
Author:			Juan Carlos Velazquez
Version:		v1.2.1
Description:	Se modifican archivos bat Instalador, Reinicia CreaTarea service se cambia la ruta donde estara encontrando el archivo .zip para la instalacion y actualziacion de posibles liberaciones, 
				la ruta sera sfspos\serviceUpdate
Release			true

Date:			20240918
Author:			Juan Carlos Velazquez
Version:		v1.2.2
Description:	Se agrega procesos default. Se actuliza el tiempo para la detencion de proceso en caso de algun error a dos minutos
Release			true

Date:			20241004
Author:			Juan Carlos Velazquez
Version:		v1.2.3
Description:	Se cambia el numero de version para monitorear el proceso de auto-actualizacion 
Release			true