#include <inttypes.h>
#include <string.h>
#include <dirent.h>
#include <sys/sysmacros.h>
#include <sys/stat.h>
#include <sys/mount.h>
#include <errno.h>

#define DEV_MOUNTPATH "/dev/"

struct major_minor {
    uint32_t major;
    uint32_t minor;
};

int find_dev_path_from_major_minor(struct major_minor dev, char *path, int path_size) {
    DIR *dev_dir;
    dev_dir = opendir(DEV_MOUNTPATH);
    if (!dev_dir) {
        return -errno;
    }

    int ret = -ENOENT;
    struct stat st;
    struct dirent *file;
    char file_path[264];
    memcpy(file_path, DEV_MOUNTPATH, sizeof(DEV_MOUNTPATH) - 1);

    while ((file = readdir(dev_dir)) != NULL) {
        strncpy(file_path + (sizeof(DEV_MOUNTPATH) - 1), file->d_name, sizeof(file_path) - (sizeof(DEV_MOUNTPATH) - 1));
        if (stat(file_path, &st) == 0 && major(st.st_rdev) == dev.major && minor(st.st_rdev) == dev.minor) {
            strncpy(path, file_path, path_size);
            ret = 0;
            break;
        }
    }

    closedir(dev_dir);
    return ret;
}

int mount_ffs(const char *func_name) {
    char target[256];
    memcpy(target, DEV_MOUNTPATH, sizeof(DEV_MOUNTPATH) - 1);
    strncpy(target + (sizeof(DEV_MOUNTPATH) - 1), func_name, sizeof(target) - (sizeof(DEV_MOUNTPATH) - 1));

    int ret = mkdir(target, S_IRWXU | S_IRGRP | S_IXGRP);
    if (ret != 0) {
        ret = -errno;
        if (ret != -EEXIST) {
            return ret;
        }
    }

    ret = mount(func_name, target, "functionfs", 0, NULL);
    if (ret != 0) {
        ret = -errno;
    }

    return ret == -EBUSY ? 0 : ret;
}
