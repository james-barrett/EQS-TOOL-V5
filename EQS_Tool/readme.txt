General usage

The app includes a file App.config for various user set options

Electrical supervisor team will approve pdf electrical certificates and save them into the relevant folders which are 
automatically created if missing in the directory alongside this executable.

When ready to process, the application is run which uses a predifend list of sub folders EH, FWT, RR 
and collects all pdf files, looks within the pdf file for certain keywords to define the certificate type 
reads data such as uprn, certificate number, address, postcode, job reference, certificate date,
engineer name, supervisor name if certificate type is and eicr also grab the satisfactory/unsatisfactory result.

If the certificate type in invalid or not found the file is moved to the error folder.

We then load up an excel file which contains an upto date list of properties with uprn and address data, we then check 
the uprn and address from the certificate match within a certain score the uprn and address from the excel file.

If the address match is succesful we move onto renaming and moving the file to the preset locations, if the address match
fails we move the file to the address_check_failed folder.

During processing if we get file error due to corruption for example we move the file to the error folder, if we
fail to match a uprn with the excel file likely due to the property been new and not included within the file we move the 
certificate to the uprn_error folder, if we set the config to not auto move files they will be moved to the processed 
folder if renaming is successful

During processing of eicr files from within the FWT folder we generate a text file containing the needed data to process
on accuserv

On successful processing of empty homes files they are emailed to the email address from within the config file, this is needed to allow them to process the voids.

Rect data has been moved to the config file to hopefully allow updates without needing an application rebuild.


