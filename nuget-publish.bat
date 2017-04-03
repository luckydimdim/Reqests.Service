set project_name=Requests.Service
set configuration=Debug
set nupkg_out_dir=bin\%configuration%
set nuget_source=http://cm-ylng-msk-04/nuget/nuget

dotnet clean %project_name%\%project_name%.csproj --configuration %configuration%
dotnet restore %project_name%\%project_name%.csproj --source %nuget_source% --no-cache
dotnet build %project_name%\%project_name%.csproj --configuration %configuration%
dotnet pack %project_name%\%project_name%.csproj --output %nupkg_out_dir% --include-source --configuration %configuration%
dotnet nuget push %project_name%\%nupkg_out_dir%\*.nupkg --source %nuget_source%

pause