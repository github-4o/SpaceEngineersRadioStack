#!/bin/bash

if [ "$1" == "-c" ]; then
    if [ ! -e doxygen ]; then
        mkdir doxygen
    fi
    echo "building docs"
    doxygen.exe doxyconfig doxygen/
else

    SOURCES=`find src/ -name '*.cs'`
    TESTBENCH=`find tb/ -name '*.cs'`

    mcs ProtoTest.cs $TESTBENCH $SOURCES -out:ProtoTest.exe
    if [ "$?" != "0" ]; then
        echo "mcs failed"
        exit 1
    else
        echo "mcs ok"
    fi

    ./ProtoTest.exe
fi
