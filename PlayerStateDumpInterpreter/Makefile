# Makefile for unix systems
# this requires GNU make

APPNAME=dump_interpreter

# enable all warnings in general
WARNFLAGS= -Wall -Wextra -Wno-unused-parameter

# base flags, used for all builds
BASECXXFLAGS = -std=c++11 # we use some C++ 11 constructs in the code ...

# optimize flags, only used for RELEASE=1 builds
# for OpenMP, you might want to add -fopenmp
OPTIMIZEFLAGS = -ffast-math -mtune=native -march=native -flto -O4 -DNDEBUG

ifeq ($(RELEASE), 1)
#
CFLAGS =   $(BASECFLAGS) $(WARNFLAGS) $(OPTIMIZEFLAGS) -s
CXXFLAGS = $(BASECXXFLAGS) $(WARNFLAGS) $(OPTIMIZEFLAGS) -s
else
CFLAGS =   $(BASECFLAGS) $(WARNFLAGS) -Werror -g
CXXFLAGS = $(BASECXXFLAGS) $(WARNFLAGS) -Werror -g
endif

# all needed libraries
LINK = -lm

# Files

# "shared" files are all files needed both for the uebung and the project
CPPFILES=$(wildcard *.cpp)
INCFILES=$(wildcard *.h)
SRCFILES=$(CFILES) $(CPPFILES)
OBJECTS =$(patsubst %.cpp,%.o,$(CPPFILES)) $(patsubst %.c,%.o,$(CFILES))
PRJFILES=Makefile $(wildcard *.sln)

# build rules
.PHONY: all
all:	$(APPNAME)

# automatic dependency generation
# create $(DEPDIR) (and an empty file dir)
# create a .d file for every .c/.cpp source file which contains
# 		   all dependencies for that file
.PHONY: depend
depend:	$(DEPDIR)/dependencies
DEPDIR   = ./dep
DEPFILES = $(patsubst %.c,$(DEPDIR)/%.d,$(CFILES)) $(patsubst %.cpp,$(DEPDIR)/%.d,$(CPPFILES) $(CPPFILES))
$(DEPDIR)/dependencies: $(DEPDIR)/dir $(DEPFILES)
	@cat $(DEPFILES) > $(DEPDIR)/dependencies
$(DEPDIR)/dir:
	@mkdir -p $(DEPDIR)
	@mkdir -p $(DEPDIR)/glad/src
	@mkdir -p $(DEPDIR)/vd
	@touch $(DEPDIR)/dir
$(DEPDIR)/%.d: %.c $(DEPDIR)/dir
	@echo rebuilding dependencies for $*
	@set -e; $(CC) -M $(CPPFLAGS) $<	\
	| sed 's,\($*\)\.o[ :]*,\1.o $@ : ,g' \
	> $@; [ -s $@ ] || rm -f $@
$(DEPDIR)/%.d: %.cpp $(DEPDIR)/dir
	@echo rebuilding dependencies for $*
	@set -e; $(CXX) -M $(CPPFLAGS) $<	\
	| sed 's,\($*\)\.o[ :]*,\1.o $@ : ,g' \
	> $@; [ -s $@ ] || rm -f $@
-include $(DEPDIR)/dependencies

# rules to build applications
$(APPNAME): $(OBJECTS) $(DEPDIR)/dependencies
	$(CXX) $(CFLAGS) $(OBJECTS) $(LDFLAGS) $(LINK) -o$(APPNAME)

# remove all unneeded files
.PHONY: clean
clean:
	@echo removing binaries: $(APPNAME)
	@rm -f $(APPNAME)
	@echo removing object files: $(OBJECTS)
	@rm -f $(OBJECTS)
	@echo removing dependency files
	@rm -rf $(DEPDIR)
	@echo removing tags
	@rm -f tags

# update the tags file
.PHONY: TAGS
TAGS:	tags

tags:	$(SRCFILES) $(INCFILES) 
	ctags $(SRCFILES) $(INCFILES)

# look for 'TODO' in all relevant files
.PHONY: todo
todo:
	-egrep -in "TODO" $(SRCFILES)$(INCFILES)

