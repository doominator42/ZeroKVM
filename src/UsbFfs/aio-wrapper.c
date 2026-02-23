#include <stddef.h>
#include <stdlib.h>
#include <unistd.h>
#include <sys/eventfd.h>
#include <libaio.h>
#include <errno.h>
#include <stdio.h>

#define container_of(ptr, type, member) ({ \
	const typeof( ((type *)0)->member ) *__mptr = (ptr); \
	(type *)( (char *)__mptr - offsetof(type,member) );})

#define IOCB_FLAG_RESFD (1<<0)

struct ep_buf {
	struct iocb cb;
	int size;
	int offset;
	char* buf;
};

struct ep {
    int file;
	int buf_count;
	struct ep_buf *bufs;
};

struct ctx {
    io_context_t ioctx;
    int eventfd;
	int max_events;
	struct io_event *events;
	int submit_cbs_count;
	struct iocb **submit_cbs;
    int ep_count;
	struct ep *eps;
};

struct ctx* ffs_aio_init(int ep_count, int max_events) {
	int ret;
	struct ctx* ctx = malloc(sizeof(*ctx));
	if (!ctx) {
		return (struct ctx*)(void*)-ENOMEM;
	}

    memset(&ctx->ioctx, 0, sizeof(ctx->ioctx));

    ret = io_setup(max_events, &ctx->ioctx);
    if (ret < 0) {
        free(ctx);
		// io_setup from libaio.h already returns -errno
        return (struct ctx*)(intptr_t)ret;
    }

    ctx->eventfd = eventfd(0, 0);
    if (ctx->eventfd < 0) {
        free(ctx);
        return (struct ctx*)(intptr_t)-errno;
    }

	ctx->max_events = max_events;
    ctx->events = malloc(max_events * sizeof(*ctx->events));
    if (!ctx->events) {
        free(ctx);
        return (struct ctx*)(void*)-ENOMEM;
    }

	ctx->submit_cbs_count = 0;
    ctx->submit_cbs = malloc(max_events * sizeof(*ctx->submit_cbs));
    if (!ctx->submit_cbs) {
		free(ctx->events);
        free(ctx);
        return (struct ctx*)(void*)-ENOMEM;
    }

    ctx->ep_count = ep_count;
	ctx->eps = malloc(ep_count * sizeof(*ctx->eps));
	if (!ctx->eps) {
		free(ctx->events);
		free(ctx->submit_cbs);
		free(ctx);
		return (struct ctx*)(void*)-ENOMEM;
	}

	memset(ctx->eps, 0, ep_count * sizeof(*ctx->eps));
	return ctx;
}

int ffs_aio_init_ep(struct ctx *ctx, int ep_index, int fd, int buf_count, int buf_size, int buf_offset, int is_write) {
	struct ep *ep = &ctx->eps[ep_index];
	ep->file = fd;
	ep->buf_count = buf_count;
	struct ep_buf *bufs = malloc(buf_count * sizeof(*bufs));
	if (!bufs) {
		return -ENOMEM;
	}

	ep->bufs = bufs;
	memset(bufs, 0, buf_count * sizeof(*bufs));

	for (int i = 0; i < buf_count; i++) {
		bufs[i].size = buf_size - buf_offset;
		bufs[i].offset = buf_offset;
		bufs[i].buf = malloc(buf_size);
		if (!bufs[i].buf) {
			for (int j = i - 1; j >= 0; j--) {
				free(bufs[j].buf);
				bufs[j].buf = NULL;
			}

			free(bufs);
			ep->bufs = NULL;
			return -ENOMEM;
		}

		if (is_write) {
			io_prep_pwrite(&bufs[i].cb, fd, bufs[i].buf + buf_offset, buf_size - buf_offset, 0);
		} else {
			io_prep_pread(&bufs[i].cb, fd, bufs[i].buf + buf_offset, buf_size - buf_offset, 0);
		}

		bufs[i].cb.u.c.flags |= IOCB_FLAG_RESFD;
		bufs[i].cb.u.c.resfd = ctx->eventfd;
		ctx->submit_cbs[ctx->submit_cbs_count] = &bufs[i].cb;
		ctx->submit_cbs_count++;
	}

	return 0;
}

int ffs_aio_read_events(struct ctx* ctx) {
    int ret;
    uint64_t event_count;
    ret = (int)read(ctx->eventfd, &event_count, sizeof(event_count));
    if (ret < 0) {
        return -errno;
    }

	// io_getevents from libaio.h already returns -errno
    return io_getevents(ctx->ioctx, 1, ctx->max_events, ctx->events, NULL);
}

struct __attribute__((__packed__)) event_data {
	int32_t fd;
	int32_t result;
	int32_t offset;
	char* buf;
};

void ffs_aio_event_data(struct ctx* ctx, int event_index, struct event_data *data) {
	struct io_event *event = &ctx->events[event_index];
	struct ep_buf *buf = container_of(event->obj, struct ep_buf, cb);
	data->fd = event->obj->aio_fildes;
	data->result = event->res;
	data->offset = buf->offset;
	data->buf = buf->buf;
}

void ffs_aio_prep_read(struct ctx *ctx, int event_index) {
	struct io_event *event = &ctx->events[event_index];
	struct ep_buf *buf = container_of(event->obj, struct ep_buf, cb);
	io_prep_pread(&buf->cb, event->obj->aio_fildes, buf->buf + buf->offset, buf->size, 0);
	buf->cb.u.c.flags |= IOCB_FLAG_RESFD;
	buf->cb.u.c.resfd = ctx->eventfd;

	ctx->submit_cbs[ctx->submit_cbs_count] = &buf->cb;
	ctx->submit_cbs_count++;
}

void ffs_aio_prep_write(struct ctx *ctx, int event_index) {
	struct io_event *event = &ctx->events[event_index];
	struct ep_buf *buf = container_of(event->obj, struct ep_buf, cb);
	io_prep_pwrite(&buf->cb, event->obj->aio_fildes, buf->buf + buf->offset, buf->size, 0);
	buf->cb.u.c.flags |= IOCB_FLAG_RESFD;
	buf->cb.u.c.resfd = ctx->eventfd;

	ctx->submit_cbs[ctx->submit_cbs_count] = &buf->cb;
	ctx->submit_cbs_count++;
}

int ffs_aio_submit(struct ctx *ctx) {
	if (ctx->submit_cbs_count > 0) {
		int ret = io_submit(ctx->ioctx, ctx->submit_cbs_count, ctx->submit_cbs);
		if (ret < 0) {
			// io_submit from libaio.h already returns -errno
			return ret;
		}

		ctx->submit_cbs_count = 0;
	}

	return 0;
}

void ffs_aio_free(struct ctx* ctx) {
    io_destroy(ctx->ioctx);
	free(ctx->events);

    for (int i = 0; i < ctx->ep_count; i++) {
		struct ep_buf *bufs = ctx->eps[i].bufs;
        if (bufs) {
			int count = ctx->eps[i].buf_count;
			for (int j = 0; j < count; j++) {
				free(bufs[j].buf);
			}

			free(bufs);
		}
    }

	free(ctx->submit_cbs);
	free(ctx->eps);
    free(ctx);
}
