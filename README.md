# WSL Docker Hub CLI
A utility for deploying images from Docker Hub to WSL.

# Requirements
- Windows 10/11  
- Windows Subsystem for Linux ([ Click me to install :) ](https://www.microsoft.com/store/productId/9P9TQF7MRM4R))  

# Get Started
1. Pick the system image you want on [Docker Hub](https://hub.docker.com/search?q=).  
2. Run `wsldh install <ImageName>:<Tag> --name <CustomName> --dir <InstallDir>` to install.  

# Example
## Install the latest version of Debian
> Install to `D:\WSL\Debian` and name to `Debian-Latest`

Run  
`wsldh install debian:latest --dir D:\WSL\Debian --name Debian-Latest`  
Or  
`wsldh i debian:latest -d D:\WSL\Debian -n Debian-Latest`  

## Install the specific version of Debian
> Install Debian 8 (jessie) to `D:\WSL\Debian Jessie` and name to `Debian-8`  

Run  
`wsldh install debian:jessie --dir "D:\WSL\Debian Jessie" --name Debian-8`  
Or  
`wsldh i debian:jessie -d "D:\WSL\Debian Jessie" -n Debian-8`  

# Features
- Use filters to select images.
- Download Docker image(s) from Docker Hub Registry. (without install)  
- Install a Docker image to WSL form Docker Hub Registry.  
- Export WSL rootfs (.tar) or Docker image (.tar.gz) form WSL.  
- Simply management of WSL.  

# Usage
```
Commands:

  help, h
    Display this help.

  download, dl
    Download Docker image(s) from Docker Hub Registry

      Usage: wsldh download <image><:tag|@digest> [options] [filters]
			
      Example:
        wsldh download ubuntu:latest --output .\output\
        wsldh download ubuntu@sha256:965fbcae990b0467ed5657caceaec165018ef44a4d2d46c7cdea80a9dff0d1ea --output .\output\

      Options:
        --output, -o <path>
          (Required) Output directory.
        --all, -a
          Download all selected images. (default: download first image only)

      Filters:
        --os <os>
          Operating system filter. (eg: linux)
        --arch <arch>
          CPU architecture filter. (eg: arm64)
        --variant <variant>
          CPU variant filter. (eg: v8)

  install, i
    Install a Docker image to WSL form Docker Hub Registry.

    Usage: wsldh install <image><:tag|@digest> [options] [filters]

    Example:
      wsldh install ubuntu:latest --name Ubuntu-Latest --dir .\WSLRootFS\Ubuntu\
      wsldh install ubuntu@sha256:965fbcae990b0467ed5657caceaec165018ef44a4d2d46c7cdea80a9dff0d1ea --name Ubuntu-Latest --dir .\WSLRootFS\Ubuntu\

    Options:
      --name, -n <name>
        (Required) Custom linux distribution name.
      --dir, -d <path>
        (Required) Linux rootfs install location.

    Filters:
      --os <os>
        Operating system filter. (eg: linux)
      --arch <arch>
        CPU architecture filter. (eg: arm64)
      --variant <variant>
        CPU variant filter. (eg: v8)

  export
    Export WSL rootfs (.tar) or Docker image (.tar.gz) form WSL.

    Usage: wsldh export <distribution name> [options]

    Example:
      wsldh export Ubuntu-Latest --output .\output\ubuntu.tar.gz --compress

    Options:
      --output, -o <path>
        (Required) Output file path.
      --compress, -c
        Use GZip to compress.

  list, ls
    List all installed distribution of WSL.

  remove, rm
	Remove a distribution from WSL.

	Usage: wsldh remove <distribution name>

	Example:
	  wsldh remove Ubuntu-Latest
```
